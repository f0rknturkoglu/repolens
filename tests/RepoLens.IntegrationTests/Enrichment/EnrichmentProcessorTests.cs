using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepoLens.Application.Enrichment;
using RepoLens.Domain.Enrichment;
using RepoLens.Infrastructure.Content;
using RepoLens.Infrastructure.GitHub;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Enrichment;

/// <summary>
/// Processor-level enrichment tests against a WireMock GitHub API and the real
/// Testcontainers PostgreSQL: happy path, missing README, archived repository,
/// duplicate runs (idempotency + snapshot policy), retry/exhaustion, permanent
/// failure, and rate-limit scheduling. No real GitHub dependency.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class EnrichmentProcessorTests(PostgresContainerFixture postgres)
{
    private const string ReadmeText = "# Migra\n\nHandles zero-downtime schema changes for PostgreSQL.\n";

    private static string DetailJson(long gitHubId, int stars = 100, bool archived = false) => $$"""
        {
          "id": {{gitHubId}},
          "name": "repo-{{gitHubId}}",
          "full_name": "owner-x/repo-{{gitHubId}}",
          "owner": { "login": "owner-x" },
          "html_url": "https://github.com/owner-x/repo-{{gitHubId}}",
          "default_branch": "main",
          "language": "C#",
          "description": "A migration tool",
          "stargazers_count": {{stars}},
          "forks_count": 12,
          "open_issues_count": 3,
          "archived": {{archived.ToString().ToLowerInvariant()}},
          "fork": false,
          "license": { "spdx_id": "MIT" },
          "created_at": "2020-01-01T00:00:00Z",
          "updated_at": "2024-01-01T00:00:00Z",
          "pushed_at": "2024-05-01T00:00:00Z"
        }
        """;

    private static void MockDetail(GitHubApiMock mock, long gitHubId, int stars = 100, bool archived = false) =>
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(DetailJson(gitHubId, stars, archived)));

    private static void MockTopics(GitHubApiMock mock, long gitHubId, params string[] topics) =>
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}/topics").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(System.Text.Json.JsonSerializer.Serialize(new { names = topics })));

    private static void MockLanguages(GitHubApiMock mock, long gitHubId, string json = """{"C#": 100, "TypeScript": 40}""") =>
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}/languages").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(json));

    private static void MockReadme(GitHubApiMock mock, long gitHubId, string? body = ReadmeText) =>
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}/readme").UsingGet())
            .RespondWith(body is null
                ? Response.Create().WithStatusCode(404).WithBody("""{"message":"Not Found"}""")
                : Response.Create().WithStatusCode(200)
                    .WithHeader("content-type", "text/plain")
                    .WithBody(body));

    private async Task<RepoLensDbContext> CreateDbAsync() =>
        await EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);

    private static RepositoryEnrichmentProcessor CreateProcessor(
        GitHubApiMock mock,
        IEnrichmentJobStore store,
        EnrichmentSettings? settings = null)
    {
        var http = new HttpClient();
        var gitHub = new GitHubRepositoryClient(
            http, Options.Create(new GitHubOptions { BaseUrl = mock.BaseUrl }));
        return new RepositoryEnrichmentProcessor(
            gitHub, new MarkdigTextNormalizer(), store, settings ?? new EnrichmentSettings());
    }

    [Fact]
    public async Task Enrichment_HappyPath_PersistsMetadataTopicsLanguagesReadmeSnapshot()
    {
        const long gitHubId = 92001;
        using var mock = new GitHubApiMock();
        MockDetail(mock, gitHubId, stars: 500);
        MockTopics(mock, gitHubId, "migration", "postgres");
        MockLanguages(mock, gitHubId);
        MockReadme(mock, gitHubId);
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId, stars: 1);
        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();
        var processor = CreateProcessor(mock, store);

        await processor.ProcessAsync(job, CancellationToken.None);

        var stored = await db.Repositories.SingleAsync(r => r.GitHubId == gitHubId);
        Assert.NotNull(stored.EnrichedAtUtc);
        Assert.Equal(500, stored.Stars);
        Assert.Equal("A migration tool", stored.Description);

        var topics = (await db.RepositoryTopics.Where(t => t.RepositoryId == stored.Id).Select(t => t.Topic).ToListAsync())
            .OrderBy(t => t).ToArray();
        Assert.Equal(["migration", "postgres"], topics);

        var languages = await db.RepositoryLanguages.Where(l => l.RepositoryId == stored.Id).ToDictionaryAsync(l => l.Language, l => l.Bytes);
        Assert.Equal(100, languages["C#"]);
        Assert.Equal(40, languages["TypeScript"]);

        var readme = await db.RepositoryReadmes.SingleAsync(r => r.RepositoryId == stored.Id);
        Assert.Contains("schema changes", readme.TextContent);
        Assert.False(string.IsNullOrEmpty(readme.ContentHash));
        Assert.Equal(64, readme.ContentHash.Length);

        var completedJob = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Completed, completedJob.Status);
        Assert.NotNull(completedJob.CompletedAtUtc);

        var snapshot = Assert.Single(await db.RepositorySnapshots.Where(s => s.RepositoryId == stored.Id).ToListAsync());
        Assert.Equal(500, snapshot.Stars);
    }

    [Fact]
    public async Task Enrichment_MissingReadme_CompletesWithoutReadmeRow()
    {
        const long gitHubId = 92002;
        using var mock = new GitHubApiMock();
        MockDetail(mock, gitHubId);
        MockTopics(mock, gitHubId);
        MockLanguages(mock, gitHubId);
        MockReadme(mock, gitHubId, body: null);
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();

        await CreateProcessor(mock, store).ProcessAsync(job, CancellationToken.None);

        Assert.Empty(await db.RepositoryReadmes.ToListAsync());
        var storedJob = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Completed, storedJob.Status);
        Assert.Null(storedJob.LastError);
    }

    [Fact]
    public async Task Enrichment_ArchivedRepository_StillEnrichesFully()
    {
        const long gitHubId = 92003;
        using var mock = new GitHubApiMock();
        MockDetail(mock, gitHubId, archived: true);
        MockTopics(mock, gitHubId, "archived-topic");
        MockLanguages(mock, gitHubId);
        MockReadme(mock, gitHubId);
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();

        await CreateProcessor(mock, store).ProcessAsync(job, CancellationToken.None);

        var stored = await db.Repositories.SingleAsync(r => r.GitHubId == gitHubId);
        Assert.True(stored.IsArchived);
        Assert.NotNull(stored.EnrichedAtUtc);
        Assert.Single(await db.RepositoryTopics.Where(t => t.RepositoryId == stored.Id).ToListAsync());
    }

    [Fact]
    public async Task Enrichment_Rerun_SkipsUnchangedReadmeAndHonorsSnapshotPolicy()
    {
        const long gitHubId = 92004;
        using var mock = new GitHubApiMock();
        MockDetail(mock, gitHubId, stars: 200);
        MockTopics(mock, gitHubId, "migration", "postgres");
        MockLanguages(mock, gitHubId);
        MockReadme(mock, gitHubId);
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        var processor = CreateProcessor(mock, store);

        // Run 1.
        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        await processor.ProcessAsync((await store.ClaimDueAsync(1, CancellationToken.None)).Single(), CancellationToken.None);
        var readmeAfterFirstRun = await db.RepositoryReadmes.SingleAsync();

        // Run 2: same metrics + same README + same topics minus one, within the
        // snapshot interval → no duplicate snapshot, no readme rewrite, topics synced.
        MockTopics(mock, gitHubId, "migration");
        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        await processor.ProcessAsync((await store.ClaimDueAsync(1, CancellationToken.None)).Single(), CancellationToken.None);

        var storedId = repository.Id;
        var readmeAfterSecondRun = await db.RepositoryReadmes.SingleAsync(r => r.RepositoryId == storedId);
        Assert.Equal(readmeAfterFirstRun.FetchedAtUtc, readmeAfterSecondRun.FetchedAtUtc);
        Assert.Equal(readmeAfterFirstRun.ContentHash, readmeAfterSecondRun.ContentHash);

        Assert.Single(await db.RepositorySnapshots.Where(s => s.RepositoryId == storedId).ToListAsync());
        var topics = await db.RepositoryTopics.Where(t => t.RepositoryId == storedId).Select(t => t.Topic).ToListAsync();
        Assert.Equal(["migration"], topics);
    }

    [Fact]
    public async Task Enrichment_Persistent5xx_ExhaustsRetryBudgetThenFails()
    {
        const long gitHubId = 92005;
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("""{"message":"boom"}"""));
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        var settings = new EnrichmentSettings { MaxAttempts = 2 };
        var processor = CreateProcessor(mock, store, settings);

        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();
        await processor.ProcessAsync(job, CancellationToken.None);

        var afterFirst = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Pending, afterFirst.Status);
        Assert.Equal(EnrichmentErrorCategory.Transient, afterFirst.LastErrorCategory);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.NotNull(afterFirst.NextAttemptAtUtc);

        // Force the retry to be due and exhaust the second (final) attempt.
        await db.EnrichmentJobs.ExecuteUpdateAsync(
            j => j.SetProperty(e => e.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var rerun = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();
        Assert.Equal(2, rerun.Attempts);
        await processor.ProcessAsync(rerun, CancellationToken.None);

        var final = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Failed, final.Status);
        Assert.Equal(EnrichmentErrorCategory.Transient, final.LastErrorCategory);
        Assert.Equal("Transient GitHub errors exceeded retry budget.", final.LastError);
    }

    [Fact]
    public async Task Enrichment_RateLimited_SchedulesRetryAtGitHubReset()
    {
        const long gitHubId = 92006;
        var resetUnix = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(403)
                .WithHeader("x-ratelimit-remaining", "0")
                .WithHeader("x-ratelimit-limit", "60")
                .WithHeader("x-ratelimit-reset", resetUnix.ToString())
                .WithBody("""{"message":"rate limited"}"""));
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        var processor = CreateProcessor(mock, store);

        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();
        await processor.ProcessAsync(job, CancellationToken.None);

        var scheduled = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Pending, scheduled.Status);
        Assert.Equal(EnrichmentErrorCategory.RateLimit, scheduled.LastErrorCategory);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(resetUnix), scheduled.NextAttemptAtUtc);

        // Not due yet → nothing claimable until the reset passes.
        Assert.Empty(await store.ClaimDueAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task Enrichment_RepositoryGone_FailsPermanentlyWithoutRetry()
    {
        const long gitHubId = 92007;
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath($"/repositories/{gitHubId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404).WithBody("""{"message":"Not Found"}"""));
        await using var db = await CreateDbAsync();
        var store = new EnrichmentJobStore(db);
        var repository = await EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);

        await store.TryEnqueueAsync(repository.Id, CancellationToken.None);
        var job = (await store.ClaimDueAsync(1, CancellationToken.None)).Single();
        await CreateProcessor(mock, store).ProcessAsync(job, CancellationToken.None);

        var failed = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(EnrichmentJobStatus.Failed, failed.Status);
        Assert.Equal(EnrichmentErrorCategory.Permanent, failed.LastErrorCategory);
        Assert.Null(failed.NextAttemptAtUtc);
    }
}

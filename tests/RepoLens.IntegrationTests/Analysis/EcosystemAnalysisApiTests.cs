using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Analysis;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Analysis;

/// <summary>
/// Ecosystem analysis API tests: candidate collection across deterministic
/// query variants (deduplicated), persisted snapshots, replay by id, and the
/// rate-limit failure path. GitHub is mocked with WireMock; PostgreSQL is real.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class EcosystemAnalysisApiTests(PostgresContainerFixture postgres)
{
    private static string RepoJson(long id, string name, string language = "Go") => $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "owner-x/{{name}}",
          "owner": { "login": "owner-x" },
          "html_url": "https://github.com/owner-x/{{name}}",
          "description": "migration related project",
          "language": "{{language}}",
          "stargazers_count": 50,
          "forks_count": 3,
          "open_issues_count": 1,
          "archived": false,
          "fork": false,
          "created_at": "2021-01-01T00:00:00Z",
          "updated_at": "2024-01-01T00:00:00Z"
        }
        """;

    private static void MockVariant(GitHubApiMock mock, string q, params long[] ids)
    {
        var items = string.Join(',', ids.Select(id => RepoJson(id, $"migration-tool-{id}")));
        mock.Server.Given(Request.Create()
                .WithPath("/search/repositories")
                .WithParam("q", q)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody($$"""{"total_count": {{ids.Length}}, "items": [{{items}}]}"""));
    }

    [Fact]
    public async Task Analyze_DeduplicatesCandidatesAcrossVariantsAndPersistsSnapshot()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        // exact variant returns A+B; topic variant returns B+C (B overlaps); broad returns C+A.
        MockVariant(mock, "postgres migration", 96001, 96002);
        MockVariant(mock, "topic:postgres topic:migration", 96002, 96003);
        MockVariant(mock, "postgres migration in:name,description,readme", 96003, 96001);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/ecosystem", new { query = "postgres migration" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("completed", body.Status);
        Assert.Equal("postgres migration", body.Query);
        Assert.NotNull(body.Metrics);
        Assert.Equal(3, body.Metrics.CandidateCount);
        Assert.NotNull(body.QueryVariants);
        Assert.Equal(3, body.QueryVariants.Count);
        Assert.True(body.Clusters.Count >= 1);

        // Candidate deduplication: three unique repositories despite six hits.
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;
        await using var db = new RepoLensDbContext(options);
        Assert.Equal(3, await db.EcosystemAnalysisCandidates.CountAsync(c => c.AnalysisId == body.Id));
        Assert.Equal(3, await db.EcosystemAnalysisCandidates
            .Where(c => c.AnalysisId == body.Id)
            .Select(c => c.RepositoryId)
            .Distinct()
            .CountAsync());

        // Replay by id is stable (same metrics snapshot).
        using var replay = await client.GetAsync($"/api/analysis/ecosystem/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<AnalysisResponseJson>();
        Assert.NotNull(replayed);
        Assert.Equal(body.Metrics.CandidateCount, replayed.Metrics?.CandidateCount);
        Assert.Equal(body.Clusters.Count, replayed.Clusters.Count);
    }

    [Fact]
    public async Task Analyze_EmptyQuery_Returns400()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/ecosystem", new { query = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_GitHubRateLimited_Returns429AndRecordsFailedAnalysis()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(403)
                .WithHeader("x-ratelimit-remaining", "0")
                .WithHeader("x-ratelimit-limit", "10")
                .WithBody("""{"message":"rate limited"}"""));

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/ecosystem", new { query = "anything" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;
        await using var db = new RepoLensDbContext(options);
        var failed = await db.EcosystemAnalyses
            .OrderByDescending(a => a.Id)
            .FirstAsync(a => a.Query == "anything");
        Assert.Equal(EcosystemAnalysisStatus.Failed, failed.Status);
        Assert.Equal(EcosystemAnalysisError.GitHubRateLimited, failed.Error);
    }

    [Fact]
    public async Task GetAnalysis_UnknownId_Returns404()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/analysis/ecosystem/987654321");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class AnalysisResponseJson
    {
        public long Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Query { get; init; } = string.Empty;
        public MetricsJson? Metrics { get; init; }
        public List<ClusterJson> Clusters { get; init; } = [];
        public List<VariantJson>? QueryVariants { get; init; }

        public sealed class MetricsJson
        {
            public int CandidateCount { get; init; }
        }

        public sealed class ClusterJson
        {
            public string Label { get; init; } = string.Empty;
            public int MemberCount { get; init; }
            public List<MemberJson> Members { get; init; } = [];
        }

        public sealed class MemberJson
        {
            public long RepositoryId { get; init; }
            public double Centrality { get; init; }
        }

        public sealed record VariantJson(string Label, string Query);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Searching;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Searching;

/// <summary>
/// End-to-end hybrid search + similar-repositories API tests. Repositories are
/// seeded into PostgreSQL with search documents and pgvector embeddings; the
/// embedding provider is mocked with WireMock (POST /embeddings always returns
/// the query vector [1,0,0,0]). Covers hybrid merge, duplicate suppression,
/// keyword fallback, similar ranking with evidence, and self-exclusion.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class SearchApiTests(PostgresContainerFixture postgres)
{
    private const string Model = "test-model-4";

    private static void MockEmbeddings(GitHubApiMock mock)
    {
        mock.Server.Given(Request.Create().WithPath("/embeddings").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody("""{"data":[{"index":0,"embedding":[1,0,0,0]}],"model":"test-model-4"}"""));
    }

    private sealed class SeededRepo(long Id, string FullName)
    {
        public long Id { get; } = Id;
        public string FullName { get; } = FullName;
    }

    private static async Task<RepoLensDbContext> SeedAsync(
        string connectionString,
        CancellationToken ct)
    {
        return await Enrichment.EnrichmentTestData.CreateCleanContextAsync(connectionString);
    }

    private async Task<List<SeededRepo>> SeedThreeRepositoriesAsync(
        RepoLensDbContext db,
        CancellationToken ct)
    {
        var store = new RepositorySearchStore(db);

        // A: matches the search query by name, has a migration topic.
        // B: semantically close to A (nearly parallel embedding), shares topic.
        // C: unrelated name, orthogonal embedding.
        var a = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, 95001);
        var b = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, 95002);
        var c = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, 95003);
        await db.SaveChangesAsync();

        foreach (var (id, topic) in new[] { (a.Id, "migration"), (b.Id, "migration") })
        {
            db.RepositoryTopics.Add(new RepoLens.Domain.Enrichment.RepositoryTopic(id, topic, DateTimeOffset.UtcNow));
        }

        await SaveDocumentAndEmbeddingAsync(store, a.Id, "migra", "schema migration tool for postgres", [1f, 0f, 0f, 0f], ct);
        await SaveDocumentAndEmbeddingAsync(store, b.Id, "flywaysh", "migration runner", [0.95f, 0.05f, 0f, 0f], ct);
        await SaveDocumentAndEmbeddingAsync(store, c.Id, "weatherapp", "react weather charts", [0f, 0f, 1f, 0f], ct);
        await db.SaveChangesAsync(ct);

        return
        [
            new SeededRepo(a.Id, a.FullName),
            new SeededRepo(b.Id, b.FullName),
            new SeededRepo(c.Id, c.FullName),
        ];
    }

    private static async Task SaveDocumentAndEmbeddingAsync(
        RepositorySearchStore store,
        long repositoryId,
        string name,
        string? description,
        float[] vector,
        CancellationToken ct)
    {
        var canonical = $"{name}\n{description ?? string.Empty}";
        await store.SaveDocumentAsync(
            SearchDocument.Create(repositoryId, name, description, null, "Go", null,
                SearchDocument.Hash(canonical)), ct);
        await store.SaveEmbeddingAsync(
            repositoryId, vector, Model, SearchDocument.Hash(canonical), DateTimeOffset.UtcNow, ct);
    }

    [Fact]
    public async Task Search_HybridMerge_ReturnsRankedResultsWithoutDuplicates()
    {
        using var mock = new GitHubApiMock();
        MockEmbeddings(mock);
        await using var db = await SeedAsync(postgres.ConnectionString, CancellationToken.None);
        var repos = await SeedThreeRepositoriesAsync(db, CancellationToken.None);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web =>
            {
                web.UseSetting("Embedding:BaseUrl", mock.BaseUrl);
                web.UseSetting("Embedding:Model", Model);
                web.UseSetting("Embedding:ApiKey", "test-key");
            });
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/search/repositories?q=migra");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("Hybrid", body.Method);
        Assert.Null(body.VectorFallbackReason);

        var migra = Assert.Single(body.Items, i => i.Id == repos[0].Id);
        Assert.True(migra.Score >= body.Items.Max(i => i.Score) - 0.001,
            "the name-matching + vector-nearest repository should rank first");
        Assert.True(body.Items.All(i => body.Items.Count(x => x.Id == i.Id) == 1), "no duplicate results");
    }

    [Fact]
    public async Task Search_WithoutEmbeddingConfiguration_FallsBackToKeyword()
    {
        using var mock = new GitHubApiMock(); // unused: no embedding calls should happen
        await using var db = await SeedAsync(postgres.ConnectionString, CancellationToken.None);
        var repos = await SeedThreeRepositoriesAsync(db, CancellationToken.None);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("Embedding:Model", ""));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/search/repositories?q=migra");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("Keyword", body.Method);
        Assert.NotNull(body.VectorFallbackReason);
        Assert.Single(body.Items, i => i.Id == repos[0].Id);
        Assert.Equal(0, mock.SearchRequestCount); // embeddings never called
    }

    [Fact]
    public async Task Search_InvalidQuery_Returns400()
    {
        using var mock = new GitHubApiMock();
        await using var db = await SeedAsync(postgres.ConnectionString, CancellationToken.None);
        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/search/repositories?q=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Similar_ReturnsClosestRepositoriesExcludingSelfWithEvidence()
    {
        using var mock = new GitHubApiMock();
        MockEmbeddings(mock);
        await using var db = await SeedAsync(postgres.ConnectionString, CancellationToken.None);
        var repos = await SeedThreeRepositoriesAsync(db, CancellationToken.None);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web =>
            {
                web.UseSetting("Embedding:BaseUrl", mock.BaseUrl);
                web.UseSetting("Embedding:Model", Model);
                web.UseSetting("Embedding:ApiKey", "test-key");
            });
        using var client = application.CreateClient();

        using var response = await client.GetAsync($"/api/repositories/{repos[0].Id}/similar?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SimilarResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);

        var nearest = body.Items[0];
        Assert.Equal(repos[1].Id, nearest.Id); // B is the closest vector to A
        Assert.True(nearest.Similarity > body.Items[1].Similarity);
        Assert.DoesNotContain(body.Items, i => i.Id == repos[0].Id); // never itself
        Assert.Contains(nearest.Evidence, e => e.Contains("migration", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, body.Items.Count); // B and C only
        Assert.Contains(body.Items, i => i.Id == repos[2].Id);
    }

    [Fact]
    public async Task Similar_UnknownRepository_Returns404()
    {
        using var mock = new GitHubApiMock();
        await using var db = await SeedAsync(postgres.ConnectionString, CancellationToken.None);
        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/repositories/9999999/similar?limit=10");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class SearchResponseJson
    {
        public string Method { get; init; } = string.Empty;
        public string? VectorFallbackReason { get; init; }
        public int TotalCount { get; init; }
        public List<SearchItemJson> Items { get; init; } = [];

        public sealed class SearchItemJson
        {
            public long Id { get; init; }
            public string FullName { get; init; } = string.Empty;
            public double Score { get; init; }
        }
    }

    private sealed class SimilarResponseJson
    {
        public string Status { get; init; } = string.Empty;
        public List<SimilarItemJson> Items { get; init; } = [];

        public sealed class SimilarItemJson
        {
            public long Id { get; init; }
            public double Similarity { get; init; }
            public List<string> Evidence { get; init; } = [];
        }
    }
}

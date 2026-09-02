using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Searching;
using RepoLens.Domain.Searching;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Searching;

/// <summary>
/// Full-text + vector retrieval against real PostgreSQL: FTS weighting/filters,
/// pgvector nearest-neighbor search, embedding round-trip, model filtering, and
/// staleness listing. Documents and embeddings are written through the store
/// (no HTTP embedding provider involved).
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class RepositorySearchStoreTests(PostgresContainerFixture postgres)
{
    private const string Model = "test-model-4";

    private async Task<(RepoLensDbContext Db, RepositorySearchStore Store)> CreateAsync()
    {
        var db = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        return (db, new RepositorySearchStore(db));
    }

    private async Task<long> SeedRepositoryAsync(
        RepoLensDbContext db,
        long gitHubId,
        string name,
        string? description,
        string language,
        int stars,
        bool archived,
        string? readmeText)
    {
        var repository = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, gitHubId);
        // InsertRepositoryAsync hardcodes name/description; fix up to the seed values.
        repository.ApplyDiscoveredMetadata(new RepoLens.Domain.Discovery.Repository.RepositoryData(
            gitHubId, repository.Owner, name, $"{repository.Owner}/{name}", description,
            repository.HtmlUrl, "main", language, stars, 0, 0, archived, false, null,
            repository.CreatedAt, DateTimeOffset.UtcNow, null));
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task SaveDocAsync(
        RepositorySearchStore store,
        long repositoryId,
        string name,
        string? description,
        string topics,
        string? readme,
        CancellationToken ct)
    {
        var canonical = string.Join('\n', name, description ?? string.Empty, topics, readme ?? string.Empty);
        await store.SaveDocumentAsync(
            SearchDocument.Create(repositoryId, name, description,
                string.IsNullOrWhiteSpace(topics) ? null : topics, "Go", readme,
                SearchDocument.Hash(canonical)), ct);
    }

    [Fact]
    public async Task KeywordSearch_RanksNameHitsAboveReadmeOnlyHits()
    {
        var (db, store) = await CreateAsync();
        var repoA = await SeedRepositoryAsync(db, 94001, "migra", "like diff for schemas", "Python", 100, false, null);
        var repoB = await SeedRepositoryAsync(db, 94002, "squabbles", "unrelated", "Go", 100, false,
            "long readme that mentions migra and schema migration and migration and migration");
        await SaveDocAsync(store, repoA, "migra", "schema diffing tool", "migration postgres", null, CancellationToken.None);
        await SaveDocAsync(store, repoB, "squabbles", "unrelated", "", "migra migration repeated many times migration migration", CancellationToken.None);

        var result = await store.KeywordSearchAsync("migra", new SearchFilters(), 10, CancellationToken.None);

        // Repo A matches on the weighted name field; repo B only via README text.
        Assert.Equal(repoA, result.RepositoryIds[0]);
        Assert.Contains(repoB, result.RepositoryIds);
        Assert.True(result.Scores[0] > result.Scores[1], "name/description weight should beat README-only weight");
    }

    [Fact]
    public async Task KeywordSearch_AppliesLanguageArchivedAndMinStarsFilters()
    {
        var (db, store) = await CreateAsync();
        var go = await SeedRepositoryAsync(db, 94010, "go-tool", "go tool", "Go", 500, false, null);
        var csharp = await SeedRepositoryAsync(db, 94011, "cs-tool", "csharp tool", "C#", 500, false, null);
        var smallGo = await SeedRepositoryAsync(db, 94012, "tiny-go", "go tool", "Go", 5, false, null);
        var archivedGo = await SeedRepositoryAsync(db, 94013, "old-go", "go tool", "Go", 500, true, null);
        foreach (var (id, name, desc, lang, readme) in new[]
        {
            (go, "go-tool", "go tool", "Go", null as string),
            (csharp, "cs-tool", "csharp tool", "C#", null),
            (smallGo, "tiny-go", "go tool", "Go", null),
            (archivedGo, "old-go", "go tool", "Go", null),
        })
        {
            await SaveDocAsync(store, id, name, desc, "tool", readme, CancellationToken.None);
        }

        var languageOnly = await store.KeywordSearchAsync("tool", new SearchFilters(Language: "Go"), 10, CancellationToken.None);
        Assert.Equal(3, languageOnly.RepositoryIds.Count);
        Assert.DoesNotContain(csharp, languageOnly.RepositoryIds);

        var liveGo = await store.KeywordSearchAsync("tool", new SearchFilters(Language: "Go", Archived: false), 10, CancellationToken.None);
        Assert.Equal(2, liveGo.RepositoryIds.Count);
        Assert.DoesNotContain(archivedGo, liveGo.RepositoryIds);

        var starred = await store.KeywordSearchAsync("tool", new SearchFilters(MinStars: 400), 10, CancellationToken.None);
        Assert.Contains(go, starred.RepositoryIds);
        Assert.DoesNotContain(smallGo, starred.RepositoryIds);
    }

    [Fact]
    public async Task KeywordSearch_NoMatch_ReturnsEmpty()
    {
        var (db, store) = await CreateAsync();
        var repo = await SeedRepositoryAsync(db, 94020, "unique", "nothing", "Go", 10, false, null);
        await SaveDocAsync(store, repo, "unique", "nothing", "", null, CancellationToken.None);

        var result = await store.KeywordSearchAsync("zzz-nomatch-zzz", new SearchFilters(), 10, CancellationToken.None);

        Assert.Empty(result.RepositoryIds);
    }

    [Fact]
    public async Task VectorSearch_ReturnsNearestNeighborsInOrder()
    {
        var (db, store) = await CreateAsync();
        var target = await SeedRepositoryAsync(db, 94030, "target", "d", "Go", 10, false, null);
        var near = await SeedRepositoryAsync(db, 94031, "near", "d", "Go", 10, false, null);
        var far = await SeedRepositoryAsync(db, 94032, "far", "d", "Go", 10, false, null);
        foreach (var (id, embedding, hash) in new[]
        {
            (target, new[] { 1.0f, 0f, 0f, 0f }, "h-a"),
            (near, new[] { 0.95f, 0.05f, 0f, 0f }, "h-b"),
            (far, new[] { 0f, 0f, 1f, 0f }, "h-c"),
        })
        {
            await SaveDocAsync(store, id, "n", "d", "", null, CancellationToken.None);
            await store.SaveEmbeddingAsync(id, embedding, Model, hash, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var query = new[] { 1.0f, 0f, 0f, 0f };
        var result = await store.VectorSearchAsync(query, Model, new SearchFilters(), 10, CancellationToken.None);

        Assert.Equal(target, result.RepositoryIds[0]);
        Assert.Equal(near, result.RepositoryIds[1]);
        Assert.Equal(far, result.RepositoryIds[2]);
        Assert.True(result.Scores[0] < result.Scores[1]);
    }

    [Fact]
    public async Task VectorSearch_ExcludesOtherModelVectors()
    {
        var (db, store) = await CreateAsync();
        var sameModel = await SeedRepositoryAsync(db, 94040, "same", "d", "Go", 10, false, null);
        var otherModel = await SeedRepositoryAsync(db, 94041, "other", "d", "Go", 10, false, null);
        foreach (var (id, model) in new[] { (sameModel, Model), (otherModel, "legacy-model") })
        {
            await SaveDocAsync(store, id, "n", "d", "", null, CancellationToken.None);
            await store.SaveEmbeddingAsync(id, [1f, 0f, 0f, 0f], model, "h", DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var result = await store.VectorSearchAsync([1f, 0f, 0f, 0f], Model, new SearchFilters(), 10, CancellationToken.None);

        Assert.Equal([sameModel], result.RepositoryIds);
    }

    [Fact]
    public async Task EmbeddingRoundTrip_PreservesVector()
    {
        var (db, store) = await CreateAsync();
        var repo = await SeedRepositoryAsync(db, 94050, "round", "d", "Go", 10, false, null);
        await SaveDocAsync(store, repo, "n", "d", "", null, CancellationToken.None);
        var expected = new[] { 0.1234567f, -0.5f, 1f };
        await store.SaveEmbeddingAsync(repo, expected, Model, "hash-1", DateTimeOffset.UtcNow, CancellationToken.None);

        var vector = await store.ReadEmbeddingVectorAsync(repo, CancellationToken.None);

        Assert.NotNull(vector);
        Assert.Equal(Model, vector.Value.Model);
        Assert.Equal(expected.Length, vector.Value.Vector.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], vector.Value.Vector[i], precision: 5);
        }
    }

    [Fact]
    public async Task StaleEmbeddingDetection_FlagsChangedOrMissingVectors()
    {
        var (db, store) = await CreateAsync();
        var fresh = await SeedRepositoryAsync(db, 94060, "fresh", "d", "Go", 10, false, null);
        var stale = await SeedRepositoryAsync(db, 94061, "stale", "d", "Go", 10, false, null);
        var none = await SeedRepositoryAsync(db, 94062, "none", "d", "Go", 10, false, null);
        foreach (var (id, hash) in new[]
        {
            (fresh, "same-hash"),
            (stale, "old-hash"),
        })
        {
            var canonical = "n\nd\n\n";
            await store.SaveDocumentAsync(
                SearchDocument.Create(id, "n", "d", null, "Go", null, SearchDocument.Hash(canonical)), CancellationToken.None);
            await store.SaveEmbeddingAsync(id, [1f, 0f], Model, hash, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        // Make the "fresh" doc's content hash differ from the embedding hash.
        await store.SaveDocumentAsync(
            SearchDocument.Create(fresh, "n2", "d", null, "Go", null, SearchDocument.Hash("changed-content")), CancellationToken.None);
        await SaveDocAsync(store, none, "none", "d", "", null, CancellationToken.None);

        var staleIds = await store.ListStaleEmbeddingDocumentIdsAsync(Model, 10, CancellationToken.None);

        Assert.Contains(fresh, staleIds); // content hash changed after embedding
        Assert.Contains(none, staleIds); // never embedded
        Assert.DoesNotContain(stale, staleIds); // embedded for same content hash
    }
}

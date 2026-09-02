using RepoLens.Application.Enrichment;
using RepoLens.Application.Searching;
using RepoLens.Domain.Searching;

namespace RepoLens.UnitTests.Searching;

/// <summary>Fakes for indexer tests — no EF, no HTTP.</summary>
public sealed class RepositoryIndexerTests
{
    private sealed class FakeDetailReader(RepositoryDetailSource? source) : IRepositoryDetailReader
    {
        public Task<RepositoryDetailSource?> GetAsync(long repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult(source);
    }

    private sealed class FakeEmbeddingGenerator(string model, bool configured) : IEmbeddingGenerator
    {
        public int Calls { get; private set; }
        public string Model { get; } = model;
        public bool IsConfigured { get; } = configured;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 1f, 0f, 0f }).ToList());
        }
    }

    private sealed class FakeSearchStore : IRepositorySearchStore
    {
        public SearchDocument? Document { get; private set; }
        public float[]? SavedEmbedding { get; private set; }
        public string? SavedModel { get; private set; }
        public string? SavedHash { get; private set; }
        public int SaveDocumentCalls { get; private set; }

        public Task SaveDocumentAsync(SearchDocument document, CancellationToken cancellationToken)
        {
            Document = document;
            SaveDocumentCalls++;
            return Task.CompletedTask;
        }

        public Task SaveEmbeddingAsync(
            long repositoryId,
            float[]? embedding,
            string? model,
            string? embeddingHash,
            DateTimeOffset? createdAtUtc,
            CancellationToken cancellationToken)
        {
            SavedEmbedding = embedding;
            SavedModel = model;
            SavedHash = embeddingHash;
            return Task.CompletedTask;
        }

        public Task<SearchDocument?> GetSearchDocumentAsync(long repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult(Document);

        public Task<(float[] Vector, string Model)?> ReadEmbeddingVectorAsync(long repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult(Document is null || SavedEmbedding is null
                ? ((float[] Vector, string Model)?)null
                : (SavedEmbedding, SavedModel ?? string.Empty));

        public Task<IReadOnlyList<long>> ListStaleEmbeddingDocumentIdsAsync(
            string currentModel, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<long>>([]);

        public Task<SearchLegResult> KeywordSearchAsync(
            string query, SearchFilters filters, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new SearchLegResult([], []));

        public Task<SearchLegResult> VectorSearchAsync(
            float[] queryVector, string model, SearchFilters filters, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new SearchLegResult([], []));

        public Task<IReadOnlyList<SearchRepositorySource>> GetRepositoriesAsync(
            IReadOnlyList<long> repositoryIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchRepositorySource>>([]);

        public Task<SearchRepositorySource?> GetRepositoryWithTopicsAsync(
            long repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<SearchRepositorySource?>(null);
    }

    private static RepositoryDetailSource DetailSource(int readmeVersion = 0) => new()
    {
        Id = 1,
        GitHubId = 101,
        Owner = "xataio",
        Name = "pgroll",
        FullName = "xataio/pgroll",
        Description = "Zero-downtime migrations",
        HtmlUrl = "https://github.com/xataio/pgroll",
        DefaultBranch = "main",
        PrimaryLanguage = "Go",
        Stars = 100,
        Forks = 1,
        OpenIssues = 1,
        IsArchived = false,
        IsFork = false,
        LicenseSpdx = "MIT",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        PushedAtUtc = null,
        DiscoveredAtUtc = DateTimeOffset.UtcNow,
        EnrichedAtUtc = DateTimeOffset.UtcNow,
        Topics = ["postgres"],
        Languages = [],
        ReadmeText = readmeVersion == 0 ? "readme v0" : "readme v1",
        ReadmeContentHash = null,
        ReadmeFetchedAtUtc = null,
    };

    private static RepositoryIndexer Indexer(
        FakeSearchStore store,
        FakeEmbeddingGenerator generator,
        RepositoryDetailSource? source = null) =>
        new(new FakeDetailReader(source ?? DetailSource()), store, generator);

    [Fact]
    public async Task Reindex_FirstRun_WritesDocumentAndEmbedding()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: true);
        var indexer = Indexer(store, generator);

        var outcome = await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        Assert.Equal(IndexingOutcome.Indexed, outcome);
        Assert.NotNull(store.Document);
        Assert.NotNull(store.SavedEmbedding);
        Assert.Equal("model-1", store.SavedModel);
        Assert.Equal(store.Document!.ContentHash, store.SavedHash);
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task Reindex_UnchangedContent_DoesNotRewriteOrReembed()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: true);
        var indexer = Indexer(store, generator);

        await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);
        var firstHash = store.Document!.ContentHash;
        var firstFetch = store.Document.EmbeddingHash;

        var outcome = await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        Assert.Equal(IndexingOutcome.UpToDate, outcome);
        Assert.Equal(1, store.SaveDocumentCalls); // no rewrite
        Assert.Equal(1, generator.Calls); // no re-embedding
        Assert.Equal(firstHash, store.Document.ContentHash);
        Assert.Equal(firstFetch, store.Document.EmbeddingHash);
    }

    [Fact]
    public async Task Reindex_ChangedContent_RewritesAndReembeds()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: true);
        var indexer = Indexer(store, generator, DetailSource(readmeVersion: 0));
        await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        // Content changed upstream (README v1) → index again.
        var changedIndexer = Indexer(store, generator, DetailSource(readmeVersion: 1));
        var outcome = await changedIndexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        Assert.Equal(IndexingOutcome.Indexed, outcome);
        Assert.Equal(2, store.SaveDocumentCalls);
        Assert.Equal(2, generator.Calls);
        Assert.NotEqual("readme v0", store.Document!.ReadmeText);
    }

    [Fact]
    public async Task Reindex_WithoutEmbedFlag_WritesDocumentOnly()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: true);
        var indexer = Indexer(store, generator);

        var outcome = await indexer.ReindexIfChangedAsync(1, embed: false, CancellationToken.None);

        Assert.Equal(IndexingOutcome.IndexedWithoutEmbedding, outcome);
        Assert.Null(store.SavedEmbedding);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task Reindex_ProviderUnconfigured_IndexesWithoutEmbedding()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: false);
        var indexer = Indexer(store, generator);

        var outcome = await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        Assert.Equal(IndexingOutcome.IndexedWithoutEmbedding, outcome);
        Assert.Null(store.SavedEmbedding);
    }

    [Fact]
    public async Task Reindex_NotYetEnriched_ReturnsNotEnriched()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: true);
        var indexer = new RepositoryIndexer(new FakeDetailReader(null), store, generator);

        var outcome = await indexer.ReindexIfChangedAsync(1, embed: true, CancellationToken.None);

        Assert.Equal(IndexingOutcome.NotEnriched, outcome);
        Assert.Null(store.Document);
    }

    [Fact]
    public async Task EnsureEmbedding_WhenUnconfigured_ThrowsUnavailable()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: false);
        var indexer = Indexer(store, generator);

        await Assert.ThrowsAsync<EmbeddingUnavailableException>(() =>
            indexer.EnsureEmbeddingAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task BackfillStale_WhenUnconfigured_DoesNothing()
    {
        var store = new FakeSearchStore();
        var generator = new FakeEmbeddingGenerator("model-1", configured: false);
        var indexer = Indexer(store, generator);

        var count = await indexer.BackfillStaleEmbeddingsAsync(10, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(0, generator.Calls);
    }
}

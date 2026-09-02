using RepoLens.Application.Enrichment;
using RepoLens.Domain.Searching;

namespace RepoLens.Application.Searching;

public enum IndexingOutcome
{
    UpToDate = 0, // content hash unchanged — nothing rewritten, embedding untouched
    Indexed = 1, // document written + embedding generated
    IndexedWithoutEmbedding = 2, // document written; embedding skipped (provider unavailable)
    NotEnriched = 3, // no enriched content to index yet
}

/// <summary>
/// Keeps search documents in sync with enrichment: rewrites a document only when
/// its content hash changed and regenerates embeddings only when the hash or
/// model changed (never on unchanged content). Missing/stale embeddings can be
/// backfilled lazily, and the similar-repositories path can force generation.
/// </summary>
public sealed class RepositoryIndexer(
    IRepositoryDetailReader reader,
    IRepositorySearchStore store,
    IEmbeddingGenerator embedding)
{
    public async Task<IndexingOutcome> ReindexIfChangedAsync(
        long repositoryId,
        bool embed,
        CancellationToken cancellationToken)
    {
        var source = await reader.GetAsync(repositoryId, cancellationToken);
        if (source is null || source.EnrichedAtUtc is null)
        {
            return IndexingOutcome.NotEnriched;
        }

        var document = SearchDocumentBuilder.Build(source);
        var existing = await store.GetSearchDocumentAsync(repositoryId, cancellationToken);
        if (existing is not null && existing.ContentHash == document.ContentHash)
        {
            return IndexingOutcome.UpToDate;
        }

        await store.SaveDocumentAsync(document, cancellationToken);

        if (!embed || !embedding.IsConfigured)
        {
            return IndexingOutcome.IndexedWithoutEmbedding;
        }

        var vector = await GenerateEmbeddingAsync(document, cancellationToken);
        await store.SaveEmbeddingAsync(
            repositoryId, vector, embedding.Model, document.ContentHash, DateTimeOffset.UtcNow,
            cancellationToken);
        return IndexingOutcome.Indexed;
    }

    /// <summary>
    /// Ensures a usable (fresh, matching-model) embedding exists for a repository.
    /// Used by similar-repositories when the document is missing or stale.
    /// </summary>
    public async Task EnsureEmbeddingAsync(long repositoryId, CancellationToken cancellationToken)
    {
        if (!embedding.IsConfigured)
        {
            throw new EmbeddingUnavailableException(null);
        }

        var source = await reader.GetAsync(repositoryId, cancellationToken)
            ?? throw new EmbeddingUnavailableException(null);
        var document = SearchDocumentBuilder.Build(source);
        var existing = await store.GetSearchDocumentAsync(repositoryId, cancellationToken);

        var documentChanged = existing is null || existing.ContentHash != document.ContentHash;
        var embeddingStale = existing is null || existing.IsEmbeddingStale(embedding.Model);
        if (!documentChanged && !embeddingStale)
        {
            return;
        }

        if (documentChanged)
        {
            await store.SaveDocumentAsync(document, cancellationToken);
        }

        var vector = await GenerateEmbeddingAsync(document, cancellationToken);
        await store.SaveEmbeddingAsync(
            repositoryId, vector, embedding.Model, document.ContentHash, DateTimeOffset.UtcNow,
            cancellationToken);
    }

    /// <summary>Backfills missing/stale embeddings for a bounded set of documents.</summary>
    public async Task<int> BackfillStaleEmbeddingsAsync(int limit, CancellationToken cancellationToken)
    {
        if (!embedding.IsConfigured)
        {
            return 0;
        }

        var stale = await store.ListStaleEmbeddingDocumentIdsAsync(embedding.Model, limit, cancellationToken);
        foreach (var repositoryId in stale)
        {
            var document = await store.GetSearchDocumentAsync(repositoryId, cancellationToken);
            if (document is null)
            {
                continue;
            }

            try
            {
                var vector = await GenerateEmbeddingAsync(document, cancellationToken);
                await store.SaveEmbeddingAsync(
                    repositoryId, vector, embedding.Model, document.ContentHash,
                    DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (EmbeddingUnavailableException)
            {
                return 0; // upstream down — stop this cycle, worker will retry later
            }
        }

        return stale.Count;
    }

    private async Task<float[]> GenerateEmbeddingAsync(
        SearchDocument document,
        CancellationToken cancellationToken)
    {
        var vectors = await embedding.GenerateAsync([SearchDocumentBuilder.EmbeddingText(document)], cancellationToken);
        return vectors.FirstOrDefault() ?? throw new EmbeddingUnavailableException(null);
    }
}

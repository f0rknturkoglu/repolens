namespace RepoLens.Application.Searching;

/// <summary>API shape of GET /api/repositories/{id}/similar.</summary>
public sealed class SimilarRepositoriesResponse
{
    public required long RepositoryId { get; init; }
    /// <summary>ok | not_indexed | unavailable — explains an empty item list.</summary>
    public required string Status { get; init; }
    public string? Reason { get; init; }
    public required IReadOnlyList<SimilarRepositoryItemDto> Items { get; init; }

    public sealed class SimilarRepositoryItemDto
    {
        public required long Id { get; init; }
        public required string FullName { get; init; }
        public string? Description { get; init; }
        public string? PrimaryLanguage { get; init; }
        public required int Stars { get; init; }
        public required bool IsArchived { get; init; }
        public required double Similarity { get; init; }
        /// <summary>Readable reasons this repository is close (topics/language/etc.).</summary>
        public required IReadOnlyList<string> Evidence { get; init; }
    }
}

/// <summary>
/// Finds semantically close repositories via exact cosine search over stored
/// embeddings. Never returns the repository itself. Missing/stale embeddings are
/// regenerated on demand; when no embedding can exist (provider unavailable) the
/// response degrades to an explicit status rather than a hard error.
/// </summary>
public sealed class SimilarRepositoriesService(
    IRepositorySearchStore store,
    IEmbeddingGenerator embedding,
    RepositoryIndexer indexer)
{
    public const int DefaultLimit = 10;

    public async Task<SimilarRepositoriesResponse> GetSimilarAsync(
        long repositoryId,
        int limit,
        CancellationToken cancellationToken)
    {
        var target = await store.GetRepositoryWithTopicsAsync(repositoryId, cancellationToken);
        if (target is null)
        {
            return new SimilarRepositoriesResponse
            {
                RepositoryId = repositoryId,
                Status = "not_found",
                Items = [],
            };
        }

        if (!embedding.IsConfigured)
        {
            return new SimilarRepositoriesResponse
            {
                RepositoryId = repositoryId,
                Status = "unavailable",
                Reason = "Embedding provider not configured.",
                Items = [],
            };
        }

        try
        {
            await indexer.EnsureEmbeddingAsync(repositoryId, cancellationToken);
        }
        catch (EmbeddingUnavailableException)
        {
            return new SimilarRepositoriesResponse
            {
                RepositoryId = repositoryId,
                Status = "unavailable",
                Reason = "Embedding generation failed; repository may not be enriched yet.",
                Items = [],
            };
        }

        var document = await store.GetSearchDocumentAsync(repositoryId, cancellationToken)
            ?? throw new InvalidOperationException("Embedding ensured but document missing.");

        var vector = await store.ReadEmbeddingVectorAsync(repositoryId, cancellationToken);
        if (vector is null || vector.Value.Model != embedding.Model)
        {
            return new SimilarRepositoriesResponse
            {
                RepositoryId = repositoryId,
                Status = "unavailable",
                Reason = "No compatible embedding is available for this repository.",
                Items = [],
            };
        }

        // Fetch a candidate neighborhood from BOTH legs? No — similarity is a
        // vector-only concern by design (semantic closeness), so query the
        // vector index directly, excluding self.
        var neighbors = await store.VectorSearchAsync(
            vector.Value.Vector, embedding.Model, new SearchFilters(), limit * 4, cancellationToken);
        var neighborsWithoutSelf = neighbors.RepositoryIds
            .Where(id => id != repositoryId)
            .Take(limit)
            .ToList();

        var sources = await store.GetRepositoriesAsync(neighborsWithoutSelf, cancellationToken);
        var byId = sources.ToDictionary(s => s.Id);
        var vectorScores = neighbors.Scores;

        var items = new List<SimilarRepositoriesResponse.SimilarRepositoryItemDto>();
        for (var i = 0; i < neighborsWithoutSelf.Count; i++)
        {
            var id = neighborsWithoutSelf[i];
            if (!byId.TryGetValue(id, out var source))
            {
                continue;
            }

            items.Add(new SimilarRepositoriesResponse.SimilarRepositoryItemDto
            {
                Id = source.Id,
                FullName = source.FullName,
                Description = source.Description,
                PrimaryLanguage = source.PrimaryLanguage,
                Stars = source.Stars,
                IsArchived = source.IsArchived,
                Similarity = Math.Round(Math.Clamp(1 - vectorScores[i] / 2.0, 0, 1), 3),
                Evidence = SimilarityEvidence.Build(target, source),
            });
        }

        return new SimilarRepositoriesResponse
        {
            RepositoryId = repositoryId,
            Status = "ok",
            Items = items,
        };
    }
}

/// <summary>Deterministic, evidence-backed reasons two repositories look similar.</summary>
public static class SimilarityEvidence
{
    public static IReadOnlyList<string> Build(SearchRepositorySource target, SearchRepositorySource other)
    {
        var evidence = new List<string>(3);
        var sharedTopics = target.Topics.Intersect(other.Topics, StringComparer.Ordinal).Take(3).ToList();
        if (sharedTopics.Count > 0)
        {
            evidence.Add($"Shares {sharedTopics.Count} topic(s): {string.Join(", ", sharedTopics)}");
        }

        if (target.PrimaryLanguage is not null
            && string.Equals(target.PrimaryLanguage, other.PrimaryLanguage, StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add($"Primary language: {other.PrimaryLanguage}");
        }

        return evidence;
    }
}

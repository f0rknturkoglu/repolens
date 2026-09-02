using RepoLens.Domain.Searching;

namespace RepoLens.Application.Searching;

/// <summary>API shape of GET /api/search/repositories.</summary>
public sealed class RepositorySearchResponse
{
    public required string Query { get; init; }
    public required SearchMethod Method { get; init; }
    /// <summary>Set when the vector leg could not run and keyword results are served.</summary>
    public string? VectorFallbackReason { get; init; }
    public required SearchFiltersDto Filters { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    /// <summary>Merged candidates available (before slicing the page).</summary>
    public required int TotalCount { get; init; }
    public required IReadOnlyList<RepositorySearchItemDto> Items { get; init; }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum SearchMethod { Keyword = 0, Hybrid = 1 }

    public sealed record SearchFiltersDto(string? Language, bool? Archived, int? MinStars);

    public sealed class RepositorySearchItemDto
    {
        public required long Id { get; init; }
        public required long GitHubId { get; init; }
        public required string Owner { get; init; }
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public string? Description { get; init; }
        public required string HtmlUrl { get; init; }
        public string? PrimaryLanguage { get; init; }
        public required int Stars { get; init; }
        public required int Forks { get; init; }
        public required int OpenIssues { get; init; }
        public required bool IsArchived { get; init; }
        public string? LicenseSpdx { get; init; }
        public required DateTimeOffset UpdatedAtUtc { get; init; }
        /// <summary>Relative relevance in [0,1] (normalized RRF); best hit is 1.</summary>
        public required double Score { get; init; }
    }
}

/// <summary>
/// Hybrid (keyword + vector, RRF-merged) search over RepoLens' own index. Falls
/// back to keyword-only when embeddings are unconfigured or the provider fails —
/// search never hard-fails on the vector leg.
/// </summary>
public sealed class RepositorySearchService(
    IRepositorySearchStore store,
    IEmbeddingGenerator embedding)
{
    public const int MaxPageSize = 50;
    public const int CandidateTake = 100;

    public async Task<RepositorySearchResponse> SearchAsync(
        string query,
        SearchFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var keyword = await store.KeywordSearchAsync(query, filters, CandidateTake, cancellationToken);

        var method = RepositorySearchResponse.SearchMethod.Keyword;
        string? vectorFallbackReason = null;
        List<RankedRepository> merged;

        if (embedding.IsConfigured)
        {
            try
            {
                var vectors = await embedding.GenerateAsync([query], cancellationToken);
                var vector = await store.VectorSearchAsync(
                    vectors.First(), embedding.Model, filters, CandidateTake, cancellationToken);
                method = RepositorySearchResponse.SearchMethod.Hybrid;
                merged = ReciprocalRankFusion.Merge(keyword.RepositoryIds, vector.RepositoryIds);
            }
            catch (EmbeddingUnavailableException ex)
            {
                vectorFallbackReason = ex.Message;
                merged = ToRanks(keyword.RepositoryIds);
            }
        }
        else
        {
            vectorFallbackReason = "Embedding provider not configured.";
            merged = ToRanks(keyword.RepositoryIds);
        }

        var total = merged.Count;
        var paged = merged
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var sources = await store.GetRepositoriesAsync(paged.Select(r => r.RepositoryId).ToList(), cancellationToken);
        var byId = sources.ToDictionary(s => s.Id);
        var orderedItems = paged
            .Select(rank => byId.TryGetValue(rank.RepositoryId, out var source)
                ? ToItem(source, rank)
                : null)
            .Where(item => item is not null)
            .Cast<RepositorySearchResponse.RepositorySearchItemDto>()
            .ToList();

        return new RepositorySearchResponse
        {
            Query = query,
            Method = method,
            VectorFallbackReason = vectorFallbackReason,
            Filters = new RepositorySearchResponse.SearchFiltersDto(
                filters.Language, filters.Archived, filters.MinStars),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = orderedItems,
        };
    }

    private static List<RankedRepository> ToRanks(IReadOnlyList<long> ids) =>
        ids.Select((id, i) => new RankedRepository(id, 1 - i / (double)Math.Max(ids.Count, 1), i + 1)).ToList();

    private static RepositorySearchResponse.RepositorySearchItemDto ToItem(
        SearchRepositorySource source,
        RankedRepository rank) => new()
    {
        Id = source.Id,
        GitHubId = source.GitHubId,
        Owner = source.Owner,
        Name = source.Name,
        FullName = source.FullName,
        Description = source.Description,
        HtmlUrl = source.HtmlUrl,
        PrimaryLanguage = source.PrimaryLanguage,
        Stars = source.Stars,
        Forks = source.Forks,
        OpenIssues = source.OpenIssues,
        IsArchived = source.IsArchived,
        LicenseSpdx = source.LicenseSpdx,
        UpdatedAtUtc = source.UpdatedAtUtc,
        Score = Math.Round(rank.Score, 4),
    };
}

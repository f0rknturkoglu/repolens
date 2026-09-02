using RepoLens.Domain.Searching;

namespace RepoLens.Application.Searching;

/// <summary>
/// Ordered ranking result of one retrieval leg. For the keyword leg, Scores are
/// ts_rank values; for the vector leg, Scores are cosine distances (ascending =
/// nearest first).
/// </summary>
public sealed record SearchLegResult(IReadOnlyList<long> RepositoryIds, IReadOnlyList<double> Scores);

/// <summary>
/// Persistence port for search documents and retrieval legs (PostgreSQL FTS and
/// exact pgvector cosine search over the stored embeddings). Implemented over
/// raw SQL in Infrastructure — the tsvector/vector columns have no EF mapping.
/// </summary>
public interface IRepositorySearchStore
{
    /// <summary>Inserts or replaces the text of a repository's search document.</summary>
    Task SaveDocumentAsync(SearchDocument document, CancellationToken cancellationToken);

    /// <summary>Writes (or clears) the embedding for a document, with staleness metadata.</summary>
    Task SaveEmbeddingAsync(
        long repositoryId,
        float[]? embedding,
        string? model,
        string? embeddingHash,
        DateTimeOffset? createdAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Current search document (text + staleness metadata), if indexed.</summary>
    Task<SearchDocument?> GetSearchDocumentAsync(long repositoryId, CancellationToken cancellationToken);

    /// <summary>Embedding vector + model for one repository, if present.</summary>
    Task<(float[] Vector, string Model)?> ReadEmbeddingVectorAsync(
        long repositoryId,
        CancellationToken cancellationToken);

    /// <summary>Repositories whose embedding is missing or stale, bounded.</summary>
    Task<IReadOnlyList<long>> ListStaleEmbeddingDocumentIdsAsync(
        string currentModel,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>FTS leg: repository ids ordered by ts_rank (best first).</summary>
    Task<SearchLegResult> KeywordSearchAsync(
        string query,
        SearchFilters filters,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Vector leg: repository ids ordered by cosine distance (nearest first).</summary>
    Task<SearchLegResult> VectorSearchAsync(
        float[] queryVector,
        string model,
        SearchFilters filters,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Repository display rows for the given ids (any order).</summary>
    Task<IReadOnlyList<SearchRepositorySource>> GetRepositoriesAsync(
        IReadOnlyList<long> repositoryIds,
        CancellationToken cancellationToken);

    /// <summary>Repository + topics for similarity evidence, for one repository.</summary>
    Task<SearchRepositorySource?> GetRepositoryWithTopicsAsync(
        long repositoryId,
        CancellationToken cancellationToken);
}

/// <summary>Lightweight repository display row used by search/similarity results.</summary>
public sealed class SearchRepositorySource
{
    public required long Id { get; init; }
    public required long GitHubId { get; init; }
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public required string HtmlUrl { get; init; }
    public string? DefaultBranch { get; init; }
    public string? PrimaryLanguage { get; init; }
    public required int Stars { get; init; }
    public required int Forks { get; init; }
    public required int OpenIssues { get; init; }
    public required bool IsArchived { get; init; }
    public required bool IsFork { get; init; }
    public string? LicenseSpdx { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? PushedAtUtc { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
}

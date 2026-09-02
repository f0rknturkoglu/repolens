namespace RepoLens.Application.Enrichment;

/// <summary>Full GitHub repository metadata (detail endpoint), normalized.</summary>
public sealed class GitHubRepositoryDetail
{
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
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? PushedAt { get; init; }
    /// <summary>Topics when GitHub included them (some listing endpoints do).</summary>
    public required IReadOnlyList<string> Topics { get; init; } = [];
}

/// <summary>README content fetched as text. Null represents a missing README.</summary>
public sealed class GitHubReadmeContent
{
    public required string RawContent { get; init; }
    public required string TextContent { get; init; }
    public required string ContentHash { get; init; }
}

/// <summary>
/// Port for GitHub repository enrichment endpoints (detail, README, topics,
/// languages) and public user-repository listing. Implemented by the
/// Infrastructure HTTP adapter; errors surface as the typed exceptions from the
/// Discovery module's GitHub error set. A missing README (404) is returned as
/// null, not as an error.
/// </summary>
public interface IGitHubRepositoryClient
{
    Task<GitHubRepositoryDetail> GetRepositoryDetailAsync(long gitHubId, CancellationToken cancellationToken);

    Task<GitHubReadmeContent?> GetReadmeAsync(long gitHubId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetTopicsAsync(long gitHubId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, long>> GetLanguagesAsync(long gitHubId, CancellationToken cancellationToken);

    /// <summary>Public repositories of a user (bounded listing, newest-updated first).</summary>
    Task<IReadOnlyList<GitHubRepositoryDetail>> GetUserRepositoriesAsync(
        string username,
        int maxRepositories,
        CancellationToken cancellationToken);
}

namespace RepoLens.Application.Discovery;

/// <summary>
/// Normalized result of a GitHub repository search — the boundary between
/// GitHub-shaped DTOs (Infrastructure) and RepoLens discovery models (Domain).
/// Optional GitHub fields are already made explicit here; no raw JSON survives.
/// </summary>
public sealed class GitHubRepositorySearchResult
{
    public required IReadOnlyList<GitHubRepositorySearchItem> Items { get; init; }

    /// <summary>GitHub's reported total match count (absent when GitHub omits it).</summary>
    public int? TotalCount { get; init; }

    /// <summary>Rate-limit metadata observed on the response, when headers were present.</summary>
    public GitHubRateLimitInfo? RateLimit { get; init; }
}

public sealed class GitHubRepositorySearchItem
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
}

/// <summary>Safe subset of GitHub X-RateLimit-* headers that is fine to expose.</summary>
public sealed record GitHubRateLimitInfo(int Limit, int Remaining, DateTimeOffset? ResetAt)
{
    public bool IsExhausted => Remaining <= 0;
}

namespace RepoLens.Application.Discovery;

/// <summary>API shape returned by GET /api/discovery/repositories.</summary>
public sealed class DiscoveryResponse
{
    public required string Query { get; init; }
    public required PageMetadata Pagination { get; init; }
    public GitHubRateLimitDto? RateLimit { get; init; }
    public required IReadOnlyList<RepositoryListItemDto> Items { get; init; }
}

public sealed class PageMetadata
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    /// <summary>GitHub-reported total match count; absent when GitHub omitted it.</summary>
    public int? TotalCount { get; init; }
    /// <summary>Total items in this response page.</summary>
    public required int ItemCount { get; init; }
}

/// <summary>Safe subset of GitHub rate-limit metadata shown to API consumers.</summary>
public sealed record GitHubRateLimitDto(int Limit, int Remaining, DateTimeOffset? ResetAtUtc);

public sealed class RepositoryListItemDto
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
}

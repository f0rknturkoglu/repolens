namespace RepoLens.Application.Enrichment;

/// <summary>
/// Repository aggregate snapshot for display (metadata + topics + languages +
/// README presence). Projected by the persistence layer without leaking EF.
/// </summary>
public sealed class RepositoryDetailSource
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
    public required DateTimeOffset DiscoveredAtUtc { get; init; }
    public DateTimeOffset? EnrichedAtUtc { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
    public required IReadOnlyList<LanguageShare> Languages { get; init; }
    /// <summary>Normalized README text; null when the repository has no README.</summary>
    public string? ReadmeText { get; init; }
    public string? ReadmeContentHash { get; init; }
    public DateTimeOffset? ReadmeFetchedAtUtc { get; init; }

    public sealed record LanguageShare(string Language, long Bytes);
}

/// <summary>Port for reading one repository's enriched aggregate.</summary>
public interface IRepositoryDetailReader
{
    Task<RepositoryDetailSource?> GetAsync(long repositoryId, CancellationToken cancellationToken);
}

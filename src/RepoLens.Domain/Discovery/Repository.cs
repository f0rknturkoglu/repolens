namespace RepoLens.Domain.Discovery;

/// <summary>
/// A GitHub repository discovered by RepoLens. Identity is the GitHub numeric
/// repository id (<see cref="GitHubId"/>); the full name is unique as a
/// secondary natural key. Mutable metadata (description, stars, …) is refreshed
/// whenever the same repository is discovered again.
/// </summary>
public sealed class Repository
{
    private Repository()
    {
        // EF Core materialization.
        Owner = string.Empty;
        Name = string.Empty;
        FullName = string.Empty;
        HtmlUrl = string.Empty;
    }

    private Repository(
        long gitHubId,
        string owner,
        string name,
        string fullName,
        string? description,
        string htmlUrl,
        string? defaultBranch,
        string? primaryLanguage,
        int stars,
        int forks,
        int openIssues,
        bool isArchived,
        bool isFork,
        string? licenseSpdx,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? pushedAt,
        DateTimeOffset discoveredAtUtc)
    {
        GitHubId = gitHubId;
        Owner = owner;
        Name = name;
        FullName = fullName;
        Description = description;
        HtmlUrl = htmlUrl;
        DefaultBranch = defaultBranch;
        PrimaryLanguage = primaryLanguage;
        Stars = stars;
        Forks = forks;
        OpenIssues = openIssues;
        IsArchived = isArchived;
        IsFork = isFork;
        LicenseSpdx = licenseSpdx;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        PushedAt = pushedAt;
        DiscoveredAtUtc = discoveredAtUtc;
    }

    /// <summary>EF Core primary key.</summary>
    public long Id { get; private set; }

    /// <summary>GitHub numeric repository id — the stable identity used for deduplication.</summary>
    public long GitHubId { get; private set; }

    public string Owner { get; private set; }

    public string Name { get; private set; }

    public string FullName { get; private set; }

    public string? Description { get; private set; }

    public string HtmlUrl { get; private set; }

    public string? DefaultBranch { get; private set; }

    public string? PrimaryLanguage { get; private set; }

    public int Stars { get; private set; }

    public int Forks { get; private set; }

    public int OpenIssues { get; private set; }

    public bool IsArchived { get; private set; }

    public bool IsFork { get; private set; }

    public string? LicenseSpdx { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PushedAt { get; private set; }

    public DateTimeOffset DiscoveredAtUtc { get; private set; }

    /// <summary>When the last full enrichment completed successfully; null when never enriched.</summary>
    public DateTimeOffset? EnrichedAtUtc { get; private set; }

    /// <summary>Creates a repository row from freshly discovered GitHub data.</summary>
    public static Repository Create(RepositoryData data, DateTimeOffset discoveredAtUtc) =>
        new(
            data.GitHubId,
            data.Owner,
            data.Name,
            data.FullName,
            data.Description,
            data.HtmlUrl,
            data.DefaultBranch,
            data.PrimaryLanguage,
            data.Stars,
            data.Forks,
            data.OpenIssues,
            data.IsArchived,
            data.IsFork,
            data.LicenseSpdx,
            data.CreatedAt,
            data.UpdatedAt,
            data.PushedAt,
            discoveredAtUtc);

    /// <summary>Refreshes mutable metadata when the repository is discovered again.</summary>
    public void ApplyDiscoveredMetadata(RepositoryData data)
    {
        Owner = data.Owner;
        Name = data.Name;
        FullName = data.FullName;
        Description = data.Description;
        HtmlUrl = data.HtmlUrl;
        DefaultBranch = data.DefaultBranch;
        PrimaryLanguage = data.PrimaryLanguage;
        Stars = data.Stars;
        Forks = data.Forks;
        OpenIssues = data.OpenIssues;
        IsArchived = data.IsArchived;
        IsFork = data.IsFork;
        LicenseSpdx = data.LicenseSpdx;
        CreatedAt = data.CreatedAt;
        UpdatedAt = data.UpdatedAt;
        PushedAt = data.PushedAt;
        DiscoveredAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Refreshes mutable metadata from a full repository detail fetch.</summary>
    public void ApplyDetailMetadata(RepositoryData data, DateTimeOffset enrichedAtUtc)
    {
        ApplyDiscoveredMetadata(data);
        EnrichedAtUtc = enrichedAtUtc;
    }

    /// <summary>
    /// Values a discovery provides about a repository. Optional fields GitHub may
    /// omit are nullable; the caller guarantees the identity and naming fields.
    /// </summary>
    public sealed record RepositoryData(
        long GitHubId,
        string Owner,
        string Name,
        string FullName,
        string? Description,
        string HtmlUrl,
        string? DefaultBranch,
        string? PrimaryLanguage,
        int Stars,
        int Forks,
        int OpenIssues,
        bool IsArchived,
        bool IsFork,
        string? LicenseSpdx,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? PushedAt);
}

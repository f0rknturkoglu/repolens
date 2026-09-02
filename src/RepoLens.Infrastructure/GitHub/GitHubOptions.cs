namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// Configuration for the GitHub API adapter. The token is optional: it improves
/// rate limits in development but is never required (unauthenticated calls work).
/// Values come from the "GitHub" configuration section; the token is normally
/// supplied via the GitHub__Token environment variable and never committed.
/// </summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string BaseUrl { get; set; } = "https://api.github.com/";
    public string? Token { get; set; }
    public string UserAgent { get; set; } = "RepoLens";
}

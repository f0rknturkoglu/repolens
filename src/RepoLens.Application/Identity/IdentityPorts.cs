using RepoLens.Domain.Identity;

namespace RepoLens.Application.Identity;

/// <summary>A signed-in user (session-derived).</summary>
public sealed record CurrentUser(long UserId, long GitHubId, string Login, string? Name, string? AvatarUrl);

/// <summary>Port for user persistence (upsert on OAuth login, saved analyses).</summary>
public interface IUserStore
{
    /// <summary>Upserts a GitHub user and returns the stored row.</summary>
    Task<User> UpsertAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken);

    Task AddSavedAnalysisAsync(SavedAnalysis saved, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedAnalysis>> ListSavedAnalysesAsync(long userId, int limit, CancellationToken cancellationToken);
}

/// <summary>OAuth options (client id/secret from environment; feature off when absent).</summary>
public sealed class GitHubOAuthSettings
{
    public const string SectionName = "GitHub:OAuth";

    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    /// <summary>Absolute callback URL of this installation.</summary>
    public string CallbackUrl { get; set; } = "http://localhost:5190/api/auth/callback";
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed record GitHubOAuthUser(long GitHubId, string Login, string? Name, string? AvatarUrl);

/// <summary>Performs the OAuth code→token→user exchange against GitHub.</summary>
public interface IGitHubOAuthClient
{
    Task<GitHubOAuthUser> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}

namespace RepoLens.Domain.Identity;

/// <summary>A RepoLens user linked to a GitHub account via OAuth.</summary>
public sealed class User
{
    private User()
    {
        // EF Core materialization.
        Login = string.Empty;
    }

    private User(
        long gitHubId,
        string login,
        string? name,
        string? avatarUrl,
        DateTimeOffset createdAtUtc)
    {
        GitHubId = gitHubId;
        Login = login;
        Name = name;
        AvatarUrl = avatarUrl;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public long GitHubId { get; private set; }
    public string Login { get; private set; }
    public string? Name { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastLoginAtUtc { get; private set; }

    public static User Create(
        long gitHubId,
        string login,
        string? name,
        string? avatarUrl,
        DateTimeOffset createdAtUtc) =>
        new(gitHubId, login, name, avatarUrl, createdAtUtc);

    public void RecordLogin(string? name, string? avatarUrl, DateTimeOffset at)
    {
        Name = name ?? Name;
        AvatarUrl = avatarUrl ?? AvatarUrl;
        LastLoginAtUtc = at;
    }
}

/// <summary>A saved pointer to one of the user's analyses (history entry).</summary>
public sealed class SavedAnalysis
{
    private SavedAnalysis()
    {
        // EF Core materialization.
        Kind = string.Empty;
        Title = string.Empty;
        Status = string.Empty;
        Version = string.Empty;
    }

    public SavedAnalysis(
        long userId,
        string kind,
        long referenceId,
        string title,
        string status,
        string version,
        DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        Kind = kind;
        ReferenceId = referenceId;
        Title = title;
        Status = status;
        Version = version;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public long UserId { get; private set; }
    /// <summary>idea | ecosystem | portfolio | recommendation</summary>
    public string Kind { get; private set; }
    public long ReferenceId { get; private set; }
    public string Title { get; private set; }
    public string Status { get; private set; }
    public string Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

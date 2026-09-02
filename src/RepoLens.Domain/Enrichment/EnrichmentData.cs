namespace RepoLens.Domain.Enrichment;

/// <summary>One topic tag attached to a repository (lowercased GitHub topic name).</summary>
public sealed class RepositoryTopic
{
    private RepositoryTopic()
    {
        Topic = string.Empty;
    }

    public RepositoryTopic(long repositoryId, string topic, DateTimeOffset addedAtUtc)
    {
        RepositoryId = repositoryId;
        Topic = topic;
        AddedAtUtc = addedAtUtc;
    }

    public long Id { get; private set; }
    public long RepositoryId { get; private set; }
    public string Topic { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
}

/// <summary>Byte count per language reported by GitHub for one repository.</summary>
public sealed class RepositoryLanguage
{
    private RepositoryLanguage()
    {
        Language = string.Empty;
    }

    public RepositoryLanguage(long repositoryId, string language, long bytes, DateTimeOffset capturedAtUtc)
    {
        RepositoryId = repositoryId;
        Language = language;
        Bytes = bytes;
        CapturedAtUtc = capturedAtUtc;
    }

    public long RepositoryId { get; private set; }
    public string Language { get; private set; }
    public long Bytes { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
}

/// <summary>
/// README content for a repository (1:1). Raw markdown plus a normalized textual
/// form are stored with a content hash; the hash lets enrichment skip rewrites
/// when nothing changed and later lets search mark stale content.
/// </summary>
public sealed class RepositoryReadme
{
    private RepositoryReadme()
    {
        RawContent = string.Empty;
        TextContent = string.Empty;
        ContentHash = string.Empty;
    }

    public RepositoryReadme(
        long repositoryId,
        string rawContent,
        string textContent,
        string contentHash,
        DateTimeOffset fetchedAtUtc)
    {
        RepositoryId = repositoryId;
        RawContent = rawContent;
        TextContent = textContent;
        ContentHash = contentHash;
        FetchedAtUtc = fetchedAtUtc;
    }

    public long RepositoryId { get; private set; }
    public string RawContent { get; private set; }
    public string TextContent { get; private set; }
    public string ContentHash { get; private set; }
    public DateTimeOffset FetchedAtUtc { get; private set; }

    public void Replace(string rawContent, string textContent, string contentHash, DateTimeOffset fetchedAtUtc)
    {
        RawContent = rawContent;
        TextContent = textContent;
        ContentHash = contentHash;
        FetchedAtUtc = fetchedAtUtc;
    }
}

/// <summary>
/// A point-in-time capture of repository metrics. Repeated enrichment creates
/// snapshots over time; creation is governed by a policy (minimum interval +
/// changed metrics) so a busy repository does not produce a snapshot per minute.
/// </summary>
public sealed class RepositorySnapshot
{
    private RepositorySnapshot()
    {
    }

    public RepositorySnapshot(long repositoryId, int stars, int forks, int openIssues, DateTimeOffset capturedAtUtc)
    {
        RepositoryId = repositoryId;
        Stars = stars;
        Forks = forks;
        OpenIssues = openIssues;
        CapturedAtUtc = capturedAtUtc;
    }

    public long Id { get; private set; }
    public long RepositoryId { get; private set; }
    public int Stars { get; private set; }
    public int Forks { get; private set; }
    public int OpenIssues { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
}

namespace RepoLens.Domain.Analysis;

public enum IdeaValidationStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>
/// One idea-validation session. The input idea text (normalized for cache
/// lookup), the search plan actually used (with its source), and JSON snapshots
/// of every computed result: ecosystem metrics, clusters, competitors, novelty
/// components, and gap hypotheses. Persisting everything keeps the report
/// reproducible and re-servable from cache without new GitHub calls.
/// </summary>
public sealed class IdeaValidation
{
    public const string CurrentVersion = "1";
    public const string NoveltyFormulaVersion = "novelty-v1";

    private IdeaValidation()
    {
        // EF Core materialization.
        IdeaText = string.Empty;
        IdeaHash = string.Empty;
    }

    private IdeaValidation(string ideaText, string ideaHash, DateTimeOffset createdAtUtc)
    {
        IdeaText = ideaText;
        IdeaHash = ideaHash;
        Version = CurrentVersion;
        Status = IdeaValidationStatus.Running;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public string IdeaText { get; private set; }
    /// <summary>Stable SHA-256 over the normalized idea text — the cache key.</summary>
    public string IdeaHash { get; private set; }
    public string Version { get; private set; }
    public string NoveltyFormula { get; private set; } = NoveltyFormulaVersion;
    public IdeaValidationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? SearchPlanJson { get; private set; }
    public string? MetricsJson { get; private set; }
    public string? ClustersJson { get; private set; }
    public string? CompetitorsJson { get; private set; }
    public string? NoveltyJson { get; private set; }
    public string? GapsJson { get; private set; }

    public static IdeaValidation Start(string ideaText, string ideaHash, DateTimeOffset createdAtUtc) =>
        new(ideaText, ideaHash, createdAtUtc);

    public void Complete(
        string searchPlanJson,
        string metricsJson,
        string clustersJson,
        string competitorsJson,
        string noveltyJson,
        string gapsJson,
        DateTimeOffset completedAtUtc)
    {
        Status = IdeaValidationStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        SearchPlanJson = searchPlanJson;
        MetricsJson = metricsJson;
        ClustersJson = clustersJson;
        CompetitorsJson = competitorsJson;
        NoveltyJson = noveltyJson;
        GapsJson = gapsJson;
    }

    public void Fail(DateTimeOffset completedAtUtc)
    {
        Status = IdeaValidationStatus.Failed;
        CompletedAtUtc = completedAtUtc;
    }
}

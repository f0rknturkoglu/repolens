namespace RepoLens.Domain.Analysis;

public enum EcosystemAnalysisStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>What went wrong when an ecosystem analysis failed (safe, UI-friendly).</summary>
public enum EcosystemAnalysisError
{
    None = 0,
    GitHubRateLimited = 1,
    GitHubUnavailable = 2,
    NoCandidates = 3,
    Unknown = 4,
}

/// <summary>
/// One ecosystem analysis session: the query, the deterministic query variants
/// used, the bounded candidate repository set, and a snapshot of the computed
/// clusters + metrics. Persisting the session makes every analysis reproducible
/// and reviewable — results are never a function of data that changes later.
/// </summary>
public sealed class EcosystemAnalysis
{
    /// <summary>Analysis engine version — stored per analysis for history trust.</summary>
    public const string CurrentVersion = "1";

    private EcosystemAnalysis()
    {
        // EF Core materialization.
        Query = string.Empty;
    }

    private EcosystemAnalysis(string query, DateTimeOffset createdAtUtc)
    {
        Query = query;
        Version = CurrentVersion;
        Status = EcosystemAnalysisStatus.Running;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public string Query { get; private set; }
    public string Version { get; private set; }
    public EcosystemAnalysisStatus Status { get; private set; }
    public EcosystemAnalysisError Error { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Serialized metrics snapshot (all numbers shown to users); null until completed.</summary>
    public string? MetricsJson { get; private set; }

    /// <summary>Serialized query variants, one per GitHub search performed; null until completed.</summary>
    public string? VariantsJson { get; private set; }

    public static EcosystemAnalysis Start(string query, DateTimeOffset createdAtUtc) =>
        new(query, createdAtUtc);

    public void Complete(string metricsJson, string variantsJson, DateTimeOffset completedAtUtc)
    {
        Status = EcosystemAnalysisStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        MetricsJson = metricsJson;
        VariantsJson = variantsJson;
    }

    public void Fail(EcosystemAnalysisError error, DateTimeOffset completedAtUtc)
    {
        Status = EcosystemAnalysisStatus.Failed;
        Error = error;
        CompletedAtUtc = completedAtUtc;
    }
}

/// <summary>
/// One candidate repository in an analysis, with the query variant that found it
/// and its rank within that variant — candidate provenance is fully traceable.
/// </summary>
public sealed class EcosystemAnalysisCandidate
{
    private EcosystemAnalysisCandidate()
    {
        // EF Core materialization.
        QueryVariant = string.Empty;
    }

    public EcosystemAnalysisCandidate(
        long analysisId,
        long repositoryId,
        string queryVariant,
        int rankInVariant)
    {
        AnalysisId = analysisId;
        RepositoryId = repositoryId;
        QueryVariant = queryVariant;
        RankInVariant = rankInVariant;
    }

    public long Id { get; private set; }
    public long AnalysisId { get; private set; }
    public long RepositoryId { get; private set; }
    public string QueryVariant { get; private set; }
    public int RankInVariant { get; private set; }
}

/// <summary>
/// A cluster snapshot: connected component of the similarity graph, with member
/// repository ids + centrality in serialized form. Labels are deterministic.
/// </summary>
public sealed class EcosystemCluster
{
    private EcosystemCluster()
    {
        // EF Core materialization.
        Label = string.Empty;
        MembersJson = string.Empty;
    }

    public EcosystemCluster(long analysisId, string label, string membersJson, DateTimeOffset createdAtUtc)
    {
        AnalysisId = analysisId;
        Label = label;
        MembersJson = membersJson;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public long AnalysisId { get; private set; }
    public string Label { get; private set; }
    public string MembersJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

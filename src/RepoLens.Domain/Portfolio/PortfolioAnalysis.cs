namespace RepoLens.Domain.Portfolio;

public enum PortfolioAnalysisStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>
/// A portfolio analysis of a public GitHub username. Signal extraction is
/// deterministic (versioned taxonomy over repository metadata — language,
/// description, topics, name); no LLM and no skill claims. Coverage bands and
/// per-category evidence are snapshotted as JSON for reproducible reports.
/// </summary>
public sealed class PortfolioAnalysis
{
    public const string CurrentVersion = "1";
    public const string TaxonomyVersion = "taxonomy-v1";
    public const string MarginalValueFormulaVersion = "marginal-v1";

    private PortfolioAnalysis()
    {
        // EF Core materialization.
        Username = string.Empty;
        Version = CurrentVersion;
    }

    private PortfolioAnalysis(string username, DateTimeOffset createdAtUtc)
    {
        Username = username;
        Version = CurrentVersion;
        Status = PortfolioAnalysisStatus.Running;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public string Username { get; private set; }
    public string Version { get; private set; }
    public PortfolioAnalysisStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int AnalyzedRepositoryCount { get; private set; }
    public int TotalRepositoryCount { get; private set; }

    /// <summary>Serialized signal evidence (per repo per category).</summary>
    public string? SignalsJson { get; private set; }

    /// <summary>Serialized coverage bands + repository summaries.</summary>
    public string? CoverageJson { get; private set; }

    public static PortfolioAnalysis Start(string username, DateTimeOffset createdAtUtc) =>
        new(username, createdAtUtc);

    public void Complete(
        int analyzedRepositoryCount,
        int totalRepositoryCount,
        string signalsJson,
        string coverageJson,
        DateTimeOffset completedAtUtc)
    {
        Status = PortfolioAnalysisStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        AnalyzedRepositoryCount = analyzedRepositoryCount;
        TotalRepositoryCount = totalRepositoryCount;
        SignalsJson = signalsJson;
        CoverageJson = coverageJson;
    }

    public void Fail(DateTimeOffset completedAtUtc)
    {
        Status = PortfolioAnalysisStatus.Failed;
        CompletedAtUtc = completedAtUtc;
    }
}

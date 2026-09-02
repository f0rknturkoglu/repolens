using System.Diagnostics.Metrics;

namespace RepoLens.Application.Observability;

/// <summary>
/// RepoLens custom metrics, emitted from Application services and the worker.
/// OTel picks them up via the registered "RepoLens" meter.
/// </summary>
public static class RepoLensMetrics
{
    public const string MeterName = "RepoLens";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> EnrichmentJobsCompleted = Meter.CreateCounter<long>(
        "repolens.enrichment.jobs_completed", "jobs", "Enrichment jobs completed.");

    public static readonly Counter<long> EnrichmentJobsFailed = Meter.CreateCounter<long>(
        "repolens.enrichment.jobs_failed", "jobs", "Enrichment jobs failed.");

    public static readonly Counter<long> GitHubRateLimited = Meter.CreateCounter<long>(
        "repolens.github.rate_limited", "requests", "Requests rejected by the GitHub rate limit.");

    public static readonly Histogram<double> EnrichmentJobDuration = Meter.CreateHistogram<double>(
        "repolens.enrichment.job_duration_seconds", "s", "Enrichment job processing duration.");

    public static readonly Histogram<double> AnalysisDuration = Meter.CreateHistogram<double>(
        "repolens.analysis.duration_seconds", "s", "Analysis pipeline duration.");
}

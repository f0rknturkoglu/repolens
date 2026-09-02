namespace RepoLens.Application.Enrichment;

/// <summary>Enrichment behavior knobs; bound from the "Enrichment" config section.</summary>
public sealed class EnrichmentSettings
{
    public const string SectionName = "Enrichment";

    /// <summary>Worker poll interval when no work is due.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Jobs claimed per poll cycle.</summary>
    public int BatchSize { get; set; } = 4;

    /// <summary>Max processing attempts before a job fails permanently.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base exponential backoff for transient errors (attempt n: base * 2^(n-1)).</summary>
    public TimeSpan TransientRetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Cap for the exponential backoff.</summary>
    public TimeSpan TransientRetryMaxDelay { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Minimum gap between snapshots of the same repository.</summary>
    public TimeSpan SnapshotMinInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Minimum interval a manual repository refresh must respect.</summary>
    public TimeSpan MinRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Processing jobs older than this are reclaimed (crash recovery).</summary>
    public TimeSpan RecoverStaleAfter { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>README content stored per repository (raw characters); longer content is truncated.</summary>
    public int ReadmeMaxLength { get; set; } = 1_000_000;
}

namespace RepoLens.Domain.Enrichment;

public enum EnrichmentJobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}

/// <summary>
/// Broad failure category used for retry decisions and surfaced (safely) in UI.
/// </summary>
public enum EnrichmentErrorCategory
{
    None = 0,
    Transient = 1, // GitHub 5xx / network — retryable
    RateLimit = 2, // GitHub 403/429 — retry after the rate-limit reset
    Permanent = 3, // e.g. repository no longer exists — no further retries
}

/// <summary>
/// A persisted background job. Status transitions are:
/// Pending → Processing → Completed, or Pending → Processing → Pending (retry
/// with next_attempt_at) → … → Failed. Because every transition is persisted,
/// a worker restart never loses work: interrupted Processing jobs are swept back
/// to Pending on startup.
/// </summary>
public sealed class EnrichmentJob
{
    private EnrichmentJob()
    {
        // EF Core materialization.
    }

    private EnrichmentJob(long repositoryId, EnrichmentJobType type, DateTimeOffset createdAtUtc)
    {
        RepositoryId = repositoryId;
        Type = type;
        Status = EnrichmentJobStatus.Pending;
        Attempts = 0;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }

    public long RepositoryId { get; private set; }

    public EnrichmentJobType Type { get; private set; }

    public EnrichmentJobStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Earliest time this job may run again (retry / rate-limit wait).</summary>
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public EnrichmentErrorCategory LastErrorCategory { get; private set; }

    /// <summary>Truncated safe error message for diagnostics; never raw GitHub text.</summary>
    public string? LastError { get; private set; }

    public static EnrichmentJob ForRepository(long repositoryId, DateTimeOffset createdAtUtc) =>
        new(repositoryId, EnrichmentJobType.Repository, createdAtUtc);

    /// <summary>Claims this job for processing (Pending → Processing).</summary>
    public void Claim(DateTimeOffset now)
    {
        if (Status != EnrichmentJobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot claim a {Status} job.");
        }

        Status = EnrichmentJobStatus.Processing;
        Attempts += 1;
        StartedAtUtc = now;
        NextAttemptAtUtc = null;
    }

    /// <summary>Completes the job (Processing → Completed).</summary>
    public void Complete(DateTimeOffset now)
    {
        if (Status != EnrichmentJobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot complete a {Status} job.");
        }

        Status = EnrichmentJobStatus.Completed;
        CompletedAtUtc = now;
        LastError = null;
        LastErrorCategory = EnrichmentErrorCategory.None;
        NextAttemptAtUtc = null;
    }

    /// <summary>Schedules a retry (Processing → Pending with next_attempt_at).</summary>
    public void ScheduleRetry(
        EnrichmentErrorCategory category,
        string message,
        DateTimeOffset? nextAttemptAtUtc)
    {
        if (Status != EnrichmentJobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot schedule a retry for a {Status} job.");
        }

        Status = EnrichmentJobStatus.Pending;
        StartedAtUtc = null;
        LastErrorCategory = category;
        LastError = Truncate(message);
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    /// <summary>Fails the job permanently (Processing → Failed).</summary>
    public void Fail(EnrichmentErrorCategory category, string message, DateTimeOffset now)
    {
        if (Status != EnrichmentJobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot fail a {Status} job.");
        }

        Status = EnrichmentJobStatus.Failed;
        CompletedAtUtc = now;
        LastErrorCategory = category;
        LastError = Truncate(message);
        NextAttemptAtUtc = null;
    }

    /// <summary>Returns an interrupted job to the pending pool (crash recovery).</summary>
    public void Recover()
    {
        if (Status != EnrichmentJobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot recover a {Status} job.");
        }

        Status = EnrichmentJobStatus.Pending;
        StartedAtUtc = null;
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500];
}

public enum EnrichmentJobType
{
    Repository = 0,
}

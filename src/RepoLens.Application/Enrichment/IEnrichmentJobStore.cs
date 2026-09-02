using RepoLens.Domain.Discovery;
using RepoLens.Domain.Enrichment;

namespace RepoLens.Application.Enrichment;

/// <summary>
/// Result of one successful repository enrichment, prepared by the processor and
/// persisted atomically by the store.
/// </summary>
public sealed class RepositoryEnrichmentResult
{
    public required Repository.RepositoryData Repository { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
    public required IReadOnlyDictionary<string, long> Languages { get; init; }
    /// <summary>Newly fetched README; null when the repository has no README.</summary>
    public GitHubReadmeContent? Readme { get; init; }
    /// <summary>Snapshot to record, decided by the processor from policy + history.</summary>
    public RepositorySnapshot? Snapshot { get; init; }
}

/// <summary>
/// Job lifecycle + enrichment persistence port, implemented over EF Core.
/// All status transitions are durable: interrupted Processing jobs are swept
/// back to Pending on restart, so no work is lost.
/// </summary>
public interface IEnrichmentJobStore
{
    /// <summary>The repository a job refers to, by internal id.</summary>
    Task<Repository?> GetRepositoryAsync(long repositoryId, CancellationToken cancellationToken);

    /// <summary>Deduped single enqueue; skipped when an active (pending/processing) job exists.</summary>
    Task<bool> TryEnqueueAsync(long repositoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Bulk enqueue for newly discovered repositories: inserts pending jobs for
    /// ids that have no active job (and are never-enriched per the caller's
    /// filter). Returns the number of jobs created.
    /// </summary>
    Task<int> TryEnqueueManyAsync(IReadOnlyList<long> repositoryIds, CancellationToken cancellationToken);

    /// <summary>Atomically claims up to <paramref name="count"/> due pending jobs (marks Processing).</summary>
    Task<IReadOnlyList<EnrichmentJob>> ClaimDueAsync(int count, CancellationToken cancellationToken);

    /// <summary>Persists enrichment data and completes the job in one transaction.</summary>
    Task CompleteAsync(long jobId, RepositoryEnrichmentResult result, CancellationToken cancellationToken);

    /// <summary>Schedules a retry: job returns to Pending with next_attempt_at set.</summary>
    Task ScheduleRetryAsync(
        long jobId,
        EnrichmentErrorCategory category,
        string message,
        DateTimeOffset? nextAttemptAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Fails the job permanently with the given category and safe message.</summary>
    Task FailAsync(
        long jobId,
        EnrichmentErrorCategory category,
        string message,
        CancellationToken cancellationToken);

    /// <summary>Reclaims interrupted Processing jobs (crash recovery).</summary>
    Task<int> RecoverInterruptedAsync(TimeSpan olderThan, CancellationToken cancellationToken);

    /// <summary>Latest snapshot for a repository, if any.</summary>
    Task<(DateTimeOffset CapturedAtUtc, int Stars, int Forks, int OpenIssues)?> GetLatestSnapshotAsync(
        long repositoryId,
        CancellationToken cancellationToken);

    /// <summary>Latest job for a repository (any state), for status display.</summary>
    Task<EnrichmentJob?> GetLatestJobAsync(long repositoryId, CancellationToken cancellationToken);

    /// <summary>True when the repository has an active (pending/processing) job.</summary>
    Task<bool> HasActiveJobAsync(long repositoryId, CancellationToken cancellationToken);

    /// <summary>Scheduled refresh: enqueues never-enriched or stale repositories (bounded).</summary>
    Task<int> EnqueueStaleForRefreshAsync(TimeSpan staleEnrichmentAge, int limit, CancellationToken cancellationToken);
}

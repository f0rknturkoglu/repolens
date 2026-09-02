using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Enrichment;
using RepoLens.Domain.Discovery;
using RepoLens.Domain.Enrichment;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IEnrichmentJobStore"/>. Job claims use
/// SELECT … FOR UPDATE SKIP LOCKED inside a transaction so concurrent workers
/// never process the same job; every transition is saved before the next step,
/// and interrupted Processing jobs are recoverable.
/// </summary>
public sealed class EnrichmentJobStore(RepoLensDbContext db) : IEnrichmentJobStore
{
    public Task<Repository?> GetRepositoryAsync(long repositoryId, CancellationToken cancellationToken) =>
        db.Repositories.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

    public async Task<bool> TryEnqueueAsync(long repositoryId, CancellationToken cancellationToken)
    {
        var hasActive = await db.EnrichmentJobs.AnyAsync(
            j => j.RepositoryId == repositoryId
                 && (j.Status == EnrichmentJobStatus.Pending || j.Status == EnrichmentJobStatus.Processing),
            cancellationToken);
        if (hasActive)
        {
            return false;
        }

        db.EnrichmentJobs.Add(EnrichmentJob.ForRepository(repositoryId, DateTimeOffset.UtcNow));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent enqueue: the unique partial index
            // (one active job per repository) fired. Treat as already queued.
            return false;
        }
    }

    public async Task<int> TryEnqueueManyAsync(
        IReadOnlyList<long> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return 0;
        }

        var ids = repositoryIds.ToArray();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO enrichment_jobs (repository_id, type, status, attempts, created_at_utc)
             SELECT r.id, 0, 0, 0, now()
             FROM repositories r
             WHERE r.id = ANY({ids})
               AND NOT EXISTS (
                 SELECT 1 FROM enrichment_jobs j
                 WHERE j.repository_id = r.id AND j.status IN (0, 1))
             ON CONFLICT DO NOTHING
             """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EnrichmentJob>> ClaimDueAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var due = await db.EnrichmentJobs
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM enrichment_jobs
                 WHERE status = 0 AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= now())
                 ORDER BY id
                 LIMIT {count}
                 FOR UPDATE SKIP LOCKED
                 """)
            .ToListAsync(cancellationToken);

        if (due.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var job in due)
            {
                job.Claim(now);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return due;
    }

    public async Task CompleteAsync(
        long jobId,
        RepositoryEnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var job = await db.EnrichmentJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
        var repository = await db.Repositories.SingleAsync(
            r => r.GitHubId == result.Repository.GitHubId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        repository.ApplyDetailMetadata(result.Repository, now);
        SynchronizeTopics(repository.Id, result.Topics, now);
        SynchronizeLanguages(repository.Id, result.Languages, now);
        await SynchronizeReadmeAsync(repository.Id, result.Readme, now, cancellationToken);

        if (result.Snapshot is not null)
        {
            db.RepositorySnapshots.Add(result.Snapshot);
        }

        job.Complete(now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ScheduleRetryAsync(
        long jobId,
        EnrichmentErrorCategory category,
        string message,
        DateTimeOffset? nextAttemptAtUtc,
        CancellationToken cancellationToken)
    {
        var job = await db.EnrichmentJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
        job.ScheduleRetry(category, message, nextAttemptAtUtc);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        long jobId,
        EnrichmentErrorCategory category,
        string message,
        CancellationToken cancellationToken)
    {
        var job = await db.EnrichmentJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
        job.Fail(category, message, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RecoverInterruptedAsync(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        var interrupted = await db.EnrichmentJobs
            .Where(j => j.Status == EnrichmentJobStatus.Processing && j.StartedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var job in interrupted)
        {
            job.Recover();
        }

        if (interrupted.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return interrupted.Count;
    }

    public async Task<(DateTimeOffset CapturedAtUtc, int Stars, int Forks, int OpenIssues)?> GetLatestSnapshotAsync(
        long repositoryId,
        CancellationToken cancellationToken)
    {
        var latest = await db.RepositorySnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .OrderByDescending(s => s.CapturedAtUtc)
            .Select(s => new
            {
                s.CapturedAtUtc,
                s.Stars,
                s.Forks,
                s.OpenIssues,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null
            ? null
            : (latest.CapturedAtUtc, latest.Stars, latest.Forks, latest.OpenIssues);
    }

    public Task<EnrichmentJob?> GetLatestJobAsync(long repositoryId, CancellationToken cancellationToken) =>
        db.EnrichmentJobs
            .Where(j => j.RepositoryId == repositoryId)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveJobAsync(long repositoryId, CancellationToken cancellationToken) =>
        db.EnrichmentJobs.AnyAsync(
            j => j.RepositoryId == repositoryId
                 && (j.Status == EnrichmentJobStatus.Pending || j.Status == EnrichmentJobStatus.Processing),
            cancellationToken);

    public async Task<int> EnqueueStaleForRefreshAsync(
        TimeSpan staleEnrichmentAge, int limit, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - staleEnrichmentAge;
        var stale = await db.Repositories
            .Where(r => r.EnrichedAtUtc == null || r.EnrichedAtUtc <= cutoff)
            .Where(r => !db.EnrichmentJobs.Any(j => j.RepositoryId == r.Id
                && (j.Status == EnrichmentJobStatus.Pending
                    || j.Status == EnrichmentJobStatus.Processing
                    || j.Status == EnrichmentJobStatus.Failed)))
            .OrderBy(r => r.EnrichedAtUtc)
            .Take(limit)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        return await TryEnqueueManyAsync(stale, cancellationToken);
    }

    private void SynchronizeTopics(long repositoryId, IReadOnlyList<string> topics, DateTimeOffset now)
    {
        var incoming = topics.Select(t => t.ToLowerInvariant()).ToHashSet();
        var existing = db.RepositoryTopics
            .Where(t => t.RepositoryId == repositoryId)
            .ToDictionary(t => t.Topic);

        foreach (var topic in incoming)
        {
            if (!existing.ContainsKey(topic))
            {
                db.RepositoryTopics.Add(new RepositoryTopic(repositoryId, topic, now));
            }
        }

        foreach (var topic in existing.Values.Where(t => !incoming.Contains(t.Topic)))
        {
            db.RepositoryTopics.Remove(topic);
        }
    }

    private void SynchronizeLanguages(
        long repositoryId,
        IReadOnlyDictionary<string, long> languages,
        DateTimeOffset now)
    {
        var current = db.RepositoryLanguages
            .Where(l => l.RepositoryId == repositoryId)
            .ToList();

        db.RepositoryLanguages.RemoveRange(current);
        foreach (var (language, bytes) in languages)
        {
            db.RepositoryLanguages.Add(new RepositoryLanguage(repositoryId, language, bytes, now));
        }
    }

    private async Task SynchronizeReadmeAsync(
        long repositoryId,
        GitHubReadmeContent? readme,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.RepositoryReadmes
            .SingleOrDefaultAsync(r => r.RepositoryId == repositoryId, cancellationToken);

        if (readme is null)
        {
            // No README anymore (e.g. deleted upstream): drop stale content.
            if (existing is not null)
            {
                db.RepositoryReadmes.Remove(existing);
            }

            return;
        }

        if (existing is not null)
        {
            // Idempotency: content hash unchanged → no rewrite.
            if (string.Equals(existing.ContentHash, readme.ContentHash, StringComparison.Ordinal))
            {
                return;
            }

            existing.Replace(readme.RawContent, readme.TextContent, readme.ContentHash, now);
            return;
        }

        db.RepositoryReadmes.Add(new RepositoryReadme(
            repositoryId, readme.RawContent, readme.TextContent, readme.ContentHash, now));
    }
}

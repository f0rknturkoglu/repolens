namespace RepoLens.Application.Enrichment;

public enum EnqueueOutcome
{
    Enqueued = 0,
    AlreadyActive = 1, // pending/processing job exists — no duplicate job
    NotFound = 2,
    RecentlyRefreshed = 3, // spam guard: enriched within MinRefreshInterval
}

/// <summary>
/// Enqueues repository enrichment jobs with deduplication and spam guards:
/// never more than one active job per repository and no manual refresh spam.
/// </summary>
public sealed class RepositoryEnrichmentScheduler(
    IEnrichmentJobStore jobs,
    EnrichmentSettings settings)
{
    /// <summary>
    /// Enqueues a single repository (manual refresh / API). Outcome tells the
    /// caller whether work was created, skipped, or the repository is unknown.
    /// </summary>
    public async Task<EnqueueOutcome> EnqueueRepositoryAsync(
        long repositoryId,
        CancellationToken cancellationToken)
    {
        var repository = await jobs.GetRepositoryAsync(repositoryId, cancellationToken);
        if (repository is null)
        {
            return EnqueueOutcome.NotFound;
        }

        if (await jobs.HasActiveJobAsync(repositoryId, cancellationToken))
        {
            return EnqueueOutcome.AlreadyActive;
        }

        // Spam guard: a repository enriched moments ago needs no immediate rerun.
        if (repository.EnrichedAtUtc is { } enrichedAt
            && DateTimeOffset.UtcNow - enrichedAt < settings.MinRefreshInterval)
        {
            return EnqueueOutcome.RecentlyRefreshed;
        }

        return await jobs.TryEnqueueAsync(repositoryId, cancellationToken)
            ? EnqueueOutcome.Enqueued
            : EnqueueOutcome.AlreadyActive;
    }

    /// <summary>
    /// Auto-enqueues repositories that have never been enriched (used right after
    /// discovery). Already-enriched repositories are left alone — refreshing
    /// them is an explicit action.
    /// </summary>
    public Task<int> EnqueueNewRepositoriesAsync(
        IEnumerable<RepoLens.Domain.Discovery.Repository> repositories,
        CancellationToken cancellationToken)
    {
        var neverEnriched = repositories.Where(r => r.EnrichedAtUtc is null)
            .Select(r => r.Id)
            .ToList();
        return jobs.TryEnqueueManyAsync(neverEnriched, cancellationToken);
    }
}

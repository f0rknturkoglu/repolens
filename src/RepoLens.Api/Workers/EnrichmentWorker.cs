using System.Diagnostics;
using RepoLens.Application.Enrichment;
using RepoLens.Application.Observability;
using RepoLens.Application.Searching;

namespace RepoLens.Api.Workers;

/// <summary>
/// Polls the enrichment job table and processes due jobs with the application
/// processor. Durable by design: claims are transactional, each transition is
/// persisted, and interrupted Processing jobs are swept back to Pending at
/// startup — a restart never loses or duplicates work (idempotent writes make
/// even a repeated run safe). After each enrichment the repository is
/// (re)indexed for hybrid search when its content changed; stale embeddings are
/// backfilled on a bounded budget per cycle.
/// </summary>
public sealed class EnrichmentWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrichmentWorker> logger,
    EnrichmentSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Enrichment worker started (poll {PollSeconds}s, batch {Batch}).",
            settings.PollInterval.TotalSeconds, settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Enrichment worker cycle failed; continuing.");
            }

            try
            {
                await Task.Delay(settings.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Enrichment worker stopped.");
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IEnrichmentJobStore>();
        var processor = scope.ServiceProvider.GetRequiredService<RepositoryEnrichmentProcessor>();
        var indexer = scope.ServiceProvider.GetRequiredService<RepositoryIndexer>();

        // Startup/crash recovery: reclaim Processing jobs that have been stuck
        // longer than the stale threshold (covers restarts + crashes).
        var recovered = await jobs.RecoverInterruptedAsync(settings.RecoverStaleAfter, stoppingToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} interrupted enrichment job(s).", recovered);
        }

        // Bounded scheduled refresh: repositories whose enrichment is older than
        // the stale threshold get re-enriched (capped per cycle).
        var refreshed = await jobs.EnqueueStaleForRefreshAsync(settings.StaleEnrichmentAge, 5, stoppingToken);
        if (refreshed > 0)
        {
            logger.LogInformation("Scheduled refresh queued {Count} stale repositor(ies).", refreshed);
        }

        var claimed = await jobs.ClaimDueAsync(settings.BatchSize, stoppingToken);
        foreach (var job in claimed)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation(
                "Processing enrichment job {JobId} (attempt {Attempt}) for repository {RepositoryId}.",
                job.Id, job.Attempts, job.RepositoryId);
            await processor.ProcessAsync(job, stoppingToken);

            // Reindex only when the enrichment actually produced fresh content.
            var outcome = await indexer.ReindexIfChangedAsync(job.RepositoryId, embed: true, stoppingToken);
            if (outcome is IndexingOutcome.Indexed or IndexingOutcome.IndexedWithoutEmbedding)
            {
                logger.LogInformation(
                    "Indexed repository {RepositoryId} for search ({Outcome}).", job.RepositoryId, outcome);
            }

            stopwatch.Stop();
            var final = await jobs.GetLatestJobAsync(job.RepositoryId, stoppingToken);
            if (final?.Status == RepoLens.Domain.Enrichment.EnrichmentJobStatus.Completed)
            {
                RepoLensMetrics.EnrichmentJobsCompleted.Add(1);
            }
            else if (final?.Status == RepoLens.Domain.Enrichment.EnrichmentJobStatus.Failed)
            {
                RepoLensMetrics.EnrichmentJobsFailed.Add(1);
            }

            RepoLensMetrics.EnrichmentJobDuration.Record(stopwatch.Elapsed.TotalSeconds);
        }

        // Bounded embedding backfill for previously indexed documents that lost
        // or outgrew their vectors (e.g. provider was configured later).
        var backfilled = await indexer.BackfillStaleEmbeddingsAsync(limit: 5, stoppingToken);
        if (backfilled > 0)
        {
            logger.LogInformation("Backfilled embeddings for {Count} document(s).", backfilled);
        }
    }
}

using RepoLens.Application.Enrichment;

namespace RepoLens.Api.Workers;

/// <summary>
/// Polls the enrichment job table and processes due jobs with the application
/// processor. Durable by design: claims are transactional, each transition is
/// persisted, and interrupted Processing jobs are swept back to Pending at
/// startup — a restart never loses or duplicates work (idempotent writes make
/// even a repeated run safe).
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

        // Startup/crash recovery: reclaim Processing jobs that have been stuck
        // longer than the stale threshold (covers restarts + crashes).
        var recovered = await jobs.RecoverInterruptedAsync(settings.RecoverStaleAfter, stoppingToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} interrupted enrichment job(s).", recovered);
        }

        var claimed = await jobs.ClaimDueAsync(settings.BatchSize, stoppingToken);
        foreach (var job in claimed)
        {
            logger.LogInformation(
                "Processing enrichment job {JobId} (attempt {Attempt}) for repository {RepositoryId}.",
                job.Id, job.Attempts, job.RepositoryId);
            await processor.ProcessAsync(job, stoppingToken);
        }
    }
}

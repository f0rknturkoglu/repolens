using RepoLens.Application.Enrichment;

namespace RepoLens.Api.Endpoints;

/// <summary>Repository detail + manual enrichment endpoints.</summary>
public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/repositories");

        group.MapGet("/{repositoryId:long}", async (
            long repositoryId,
            RepositoryDetailService details,
            CancellationToken cancellationToken) =>
        {
            var result = await details.GetAsync(repositoryId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/{repositoryId:long}/refresh", async (
            long repositoryId,
            RepositoryEnrichmentScheduler scheduler,
            CancellationToken cancellationToken) =>
        {
            var outcome = await scheduler.EnqueueRepositoryAsync(repositoryId, cancellationToken);
            return outcome switch
            {
                EnqueueOutcome.Enqueued => Results.Accepted(
                    $"/api/repositories/{repositoryId}", new { Status = "queued" }),
                EnqueueOutcome.AlreadyActive => Results.Conflict(new
                {
                    Status = "in_progress",
                    Detail = "An enrichment job for this repository is already pending or running.",
                }),
                EnqueueOutcome.RecentlyRefreshed => Results.StatusCode(StatusCodes.Status429TooManyRequests),
                _ => Results.NotFound(),
            };
        }).RequireRateLimiting("expensive");

        return routes;
    }
}

using RepoLens.Application.Discovery;

namespace RepoLens.Api.Endpoints;

/// <summary>GET /api/discovery/repositories — GitHub repository search + persistence.</summary>
public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/discovery");

        group.MapGet("/repositories", async (
            string? q,
            int? page,
            int? perPage,
            RepositoryDiscoveryService discovery,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = RepositoryDiscoveryService.ValidateQuery(q);
            if (validationErrors is not null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["q"] = validationErrors.ToArray(),
                    },
                    title: "Invalid search query.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var pageValue = page is null or < RepositoryDiscoveryService.MinPage
                ? 1
                : Math.Min(page.Value, 1000);
            var pageSizeValue = perPage is null or < RepositoryDiscoveryService.MinPageSize
                ? 30
                : Math.Min(perPage.Value, RepositoryDiscoveryService.MaxPageSize);

            var result = await discovery.SearchAsync(q!, pageValue, pageSizeValue, cancellationToken);
            return Results.Ok(result);
        });

        return routes;
    }
}

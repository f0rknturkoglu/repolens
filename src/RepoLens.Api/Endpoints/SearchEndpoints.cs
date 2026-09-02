using RepoLens.Application.Searching;

namespace RepoLens.Api.Endpoints;

/// <summary>Internal hybrid search + similar-repositories endpoints.</summary>
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/search/repositories", async (
            string? q,
            string? language,
            bool? archived,
            int? minStars,
            int? page,
            int? perPage,
            RepositorySearchService search,
            CancellationToken cancellationToken) =>
        {
            var query = q?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["q"] = ["Query is required."] },
                    title: "Invalid search query.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (query.Length > 256)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["q"] = ["Query must be at most 256 characters."] },
                    title: "Invalid search query.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var pageValue = page is null or < 1 ? 1 : page.Value;
            var pageSizeValue = perPage is null or < 1
                ? 20
                : Math.Min(perPage.Value, RepositorySearchService.MaxPageSize);

            var result = await search.SearchAsync(
                query,
                new SearchFilters(language, archived, minStars),
                pageValue,
                pageSizeValue,
                cancellationToken);
            return Results.Ok(result);
        });

        routes.MapGet("/api/repositories/{repositoryId:long}/similar", async (
            long repositoryId,
            int? limit,
            SimilarRepositoriesService similar,
            CancellationToken cancellationToken) =>
        {
            var take = limit is null or < 1
                ? SimilarRepositoriesService.DefaultLimit
                : Math.Min(limit.Value, 20);
            var result = await similar.GetSimilarAsync(repositoryId, take, cancellationToken);
            return result.Status == "not_found" ? Results.NotFound() : Results.Ok(result);
        });

        return routes;
    }
}

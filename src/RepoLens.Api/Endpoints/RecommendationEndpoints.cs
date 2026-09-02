using RepoLens.Api.Identity;
using RepoLens.Application.Identity;
using RepoLens.Application.Recommendation;

namespace RepoLens.Api.Endpoints;

/// <summary>Personalized recommendation endpoints.</summary>
public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/recommendations");

        group.MapPost("", async (
            RecommendationRequestDto request,
            RecommendationService recommendations,
            AuthSessionService sessions,
            IUserStore users,
            CancellationToken cancellationToken) =>
        {
            var input = new RecommendationInput(
                request?.Goal ?? string.Empty,
                request?.Interests ?? [],
                request?.Username,
                request?.DurationWeeks);
            var errors = RecommendationService.Validate(input);
            if (errors is not null)
            {
                return Results.ValidationProblem(
                    errors.ToDictionary(e => "input", e => new[] { e }),
                    title: "Invalid recommendation request.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var response = await recommendations.RecommendAsync(input, cancellationToken);
            await AnalysisHistorySaver.SaveAsync(sessions, users, "recommendation", response.Id,
                response.Goal.Length > 120 ? response.Goal[..120] : response.Goal,
                response.Status, response.Version, cancellationToken);
            return Results.Ok(response);
        }).RequireRateLimiting("expensive");

        group.MapGet("/{requestId:long}", async (
            long requestId,
            IRecommendationStore store,
            CancellationToken cancellationToken) =>
        {
            var stored = await store.GetAsync(requestId, cancellationToken);
            if (stored is null)
            {
                return Results.NotFound();
            }

            var response = RecommendationService.FromStored(stored, servedFromCache: false);
            return Results.Ok(response);
        });

        return routes;
    }

    public sealed record RecommendationRequestDto(
        string? Goal,
        IReadOnlyList<string>? Interests,
        string? Username,
        string? DurationWeeks);
}

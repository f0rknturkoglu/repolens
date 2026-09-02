using RepoLens.Api.Identity;
using RepoLens.Application.Identity;
using RepoLens.Application.IdeaValidation;

namespace RepoLens.Api.Endpoints;

/// <summary>Idea-validation endpoints.</summary>
public static class IdeaValidationEndpoints
{
    public static IEndpointRouteBuilder MapIdeaValidationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/analysis");

        group.MapPost("/idea", async (
            IdeaRequest request,
            IdeaValidationService validations,
            IdeaValidationResultBuilder builder,
            AuthSessionService sessions,
            IUserStore users,
            CancellationToken cancellationToken) =>
        {
            var errors = IdeaValidationService.ValidateIdea(request?.Idea);
            if (errors is not null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["idea"] = errors.ToArray() },
                    title: "Invalid project idea.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var (validation, servedFromCache) = await validations.ValidateAsync(request!.Idea!, cancellationToken);
            var response = await builder.BuildAsync(validation.Id, servedFromCache, cancellationToken);
            await AnalysisHistorySaver.SaveAsync(sessions, users, "idea", validation.Id,
                validation.IdeaText.Length > 120 ? validation.IdeaText[..120] : validation.IdeaText,
                validation.Status.ToString(), validation.Version, cancellationToken);
            return Results.Ok(response);
        }).RequireRateLimiting("expensive");

        group.MapGet("/idea/{validationId:long}", async (
            long validationId,
            IdeaValidationResultBuilder builder,
            CancellationToken cancellationToken) =>
        {
            var response = await builder.BuildAsync(validationId, servedFromCache: false, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return routes;
    }

    public sealed record IdeaRequest(string? Idea);
}

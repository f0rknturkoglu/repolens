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

            var validation = await validations.ValidateAsync(request!.Idea!, cancellationToken);
            var servedFromCache = validation.CreatedAtUtc < DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2);
            var response = await builder.BuildAsync(validation.Id, servedFromCache, cancellationToken);
            return Results.Ok(response);
        });

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

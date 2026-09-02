using RepoLens.Application.IdeaValidation;
using RepoLens.Application.Portfolio;

namespace RepoLens.Api.Endpoints;

/// <summary>Portfolio analysis + marginal-value endpoints.</summary>
public static class PortfolioEndpoints
{
    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/portfolio");

        group.MapGet("/{username}", async (
            string username,
            PortfolioAnalysisService analyses,
            PortfolioResultBuilder builder,
            CancellationToken cancellationToken) =>
        {
            var errors = PortfolioAnalysisService.ValidateUsername(username);
            if (errors is not null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["username"] = errors.ToArray() },
                    title: "Invalid GitHub username.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var analysis = await analyses.AnalyzeAsync(username, cancellationToken);
            var response = await builder.BuildAsync(analysis.Id, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        group.MapPost("/{username}/marginal", async (
            string username,
            MarginalIdeaRequest request,
            PortfolioAnalysisService analyses,
            MarginalValueService marginal,
            CancellationToken cancellationToken) =>
        {
            var errors = PortfolioAnalysisService.ValidateUsername(username);
            if (errors is not null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["username"] = errors.ToArray() },
                    title: "Invalid GitHub username.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(request?.Idea))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["idea"] = ["Project idea is required."] },
                    title: "Invalid project idea.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Ensure a recent portfolio snapshot exists before computing value.
            await analyses.AnalyzeAsync(username, cancellationToken);
            var result = await marginal.ComputeAsync(username, request!.Idea!, cancellationToken);
            return Results.Ok(result);
        });

        return routes;
    }

    public sealed record MarginalIdeaRequest(string? Idea);
}

using RepoLens.Api.Identity;
using RepoLens.Application.Analysis;
using RepoLens.Application.Identity;

namespace RepoLens.Api.Endpoints;

/// <summary>Ecosystem analysis endpoints.</summary>
public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/analysis");

        group.MapPost("/ecosystem", async (
            EcosystemRequest request,
            EcosystemAnalysisService analyses,
            EcosystemAnalysisResultBuilder builder,
            AuthSessionService sessions,
            IUserStore users,
            CancellationToken cancellationToken) =>
        {
            var errors = EcosystemAnalysisService.ValidateQuery(request?.Query);
            if (errors is not null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["query"] = errors.ToArray() },
                    title: "Invalid analysis query.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var analysis = await analyses.AnalyzeAsync(request!.Query, cancellationToken);
            var response = await builder.BuildAsync(analysis.Id, cancellationToken);
            await AnalysisHistorySaver.SaveAsync(
                sessions, users, "ecosystem", analysis.Id, analysis.Query,
                analysis.Status.ToString(), analysis.Version, cancellationToken);
            return Results.Ok(response);
        }).RequireRateLimiting("expensive");

        group.MapGet("/ecosystem/{analysisId:long}", async (
            long analysisId,
            EcosystemAnalysisResultBuilder builder,
            CancellationToken cancellationToken) =>
        {
            var response = await builder.BuildAsync(analysisId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return routes;
    }

    public sealed record EcosystemRequest(string? Query);
}

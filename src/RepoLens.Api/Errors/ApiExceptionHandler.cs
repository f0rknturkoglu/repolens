using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RepoLens.Application.Discovery;

namespace RepoLens.Api.Errors;

/// <summary>
/// Maps typed upstream failures to HTTP responses with generic, safe messages.
/// Raw GitHub error text is never forwarded to clients. Every mapping exposes a
/// stable machine-readable "code" plus a human title/detail pair.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var (statusCode, code, title, detail, retryAfter) = Map(exception);
        if (statusCode is null)
        {
            return false; // Unknown: let the default handler produce a 500.
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code },
        };

        if (retryAfter is not null)
        {
            httpContext.Response.Headers.RetryAfter = retryAfter.Value.TotalSeconds.ToString("0");
        }

        httpContext.Response.StatusCode = statusCode.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int? Status, string Code, string Title, string Detail, TimeSpan? RetryAfter) Map(
        Exception exception) => exception switch
    {
        GitHubRateLimitExceededException rateLimit => (
            StatusCodes.Status429TooManyRequests,
            "github_rate_limited",
            "GitHub rate limit reached.",
            "Too many requests to the GitHub API. Wait and retry later.",
            rateLimit.ResetAt is { } reset ? reset - DateTimeOffset.UtcNow : null),
        GitHubRequestRejectedException rejected when rejected.StatusCode is 400 or 422 => (
            StatusCodes.Status400BadRequest,
            "invalid_query",
            "The search query was rejected.",
            "GitHub could not interpret the search query. Simplify it and retry.",
            null),
        GitHubRequestRejectedException rejected => (
            StatusCodes.Status502BadGateway,
            "upstream_rejected",
            "GitHub rejected the request.",
            "The upstream GitHub API rejected the request. Try again later.",
            null),
        GitHubUpstreamErrorException => (
            StatusCodes.Status502BadGateway,
            "upstream_error",
            "GitHub API error.",
            "The GitHub API returned a transient server error. Try again later.",
            null),
        GitHubUnavailableException => (
            StatusCodes.Status503ServiceUnavailable,
            "upstream_unavailable",
            "GitHub API unavailable.",
            "The GitHub API could not be reached. Try again later.",
            null),
        GitHubMalformedResponseException => (
            StatusCodes.Status502BadGateway,
            "upstream_malformed",
            "Unexpected GitHub response.",
            "The GitHub API returned an unreadable response. Try again later.",
            null),
        _ => (null, string.Empty, string.Empty, string.Empty, null),
    };
}

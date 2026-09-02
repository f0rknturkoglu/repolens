namespace RepoLens.Application.Discovery;

/// <summary>GitHub rejected the request (4xx other than rate limiting).</summary>
public sealed class GitHubRequestRejectedException(int statusCode) : Exception(
    $"GitHub rejected the request with status {(int)statusCode} ({statusCode}).")
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// GitHub returned 403 (rate limit or abuse) or 429. <see cref="ResetAt"/> is the
/// X-RateLimit-Reset timestamp when GitHub provided one.
/// </summary>
public sealed class GitHubRateLimitExceededException(DateTimeOffset? resetAt) : Exception(
    "GitHub API rate limit exceeded.")
{
    public DateTimeOffset? ResetAt { get; } = resetAt;
}

/// <summary>GitHub returned a transient 5xx that did not recover after bounded retries.</summary>
public sealed class GitHubUpstreamErrorException(int statusCode) : Exception(
    $"GitHub API returned a transient server error ({(int)statusCode}).")
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>The GitHub API could not be reached (network failure, DNS, timeout).</summary>
public sealed class GitHubUnavailableException(Exception? inner) : Exception(
    "The GitHub API is unreachable.", inner);

/// <summary>The GitHub response could not be interpreted (malformed payload).</summary>
public sealed class GitHubMalformedResponseException(Exception? inner) : Exception(
    "GitHub returned an unparseable response.", inner);

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Discovery;
using RepoLens.Application.Enrichment;

namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// Shared HTTP plumbing for GitHub API clients: request building with the
/// optional token, rate-limit header parsing, and error classification into the
/// typed exception set (never raw GitHub text). Kept in one place so search and
/// enrichment adapters behave identically.
/// </summary>
internal static class GitHubHttp
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        GitHubOptions options,
        Uri uri,
        CancellationToken cancellationToken,
        string? acceptOverride = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd(acceptOverride ?? "application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd(options.UserAgent);
            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                request.Headers.Authorization = new("Bearer", options.Token);
            }

            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubUnavailableException(ex);
        }
    }

    public static bool IsRateLimitExhausted(HttpResponseMessage response) =>
        (int)response.StatusCode == 403
        && response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues)
        && int.TryParse(remainingValues.FirstOrDefault(), out var remaining)
        && remaining == 0;

    public static GitHubRateHeaders? ReadRateHeaders(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-ratelimit-limit", out var limitValues))
        {
            return null;
        }

        long? resetUnix = null;
        if (response.Headers.TryGetValues("x-ratelimit-reset", out var resetValues)
            && long.TryParse(resetValues.FirstOrDefault(), out var reset))
        {
            resetUnix = reset;
        }

        int? remaining = null;
        if (response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues)
            && int.TryParse(remainingValues.FirstOrDefault(), out var parsedRemaining))
        {
            remaining = parsedRemaining;
        }

        return new GitHubRateHeaders(
            int.TryParse(limitValues.FirstOrDefault(), out var limit) ? limit : null,
            remaining,
            resetUnix);
    }

    public static GitHubRateLimitInfo? ToRateLimitInfo(GitHubRateHeaders? headers)
    {
        if (headers is null || headers.Limit is null || headers.Remaining is null)
        {
            return null;
        }

        return new GitHubRateLimitInfo(headers.Limit.Value, headers.Remaining.Value, ResetAt(headers));
    }

    public static GitHubRateLimitExceededException ToRateLimitException(GitHubRateHeaders? headers) =>
        new(ResetAt(headers));

    public static Exception ToException(HttpResponseMessage response) =>
        (int)response.StatusCode is >= 500 and <= 599
            ? new GitHubUpstreamErrorException((int)response.StatusCode)
            : new GitHubRequestRejectedException((int)response.StatusCode);

    private static DateTimeOffset? ResetAt(GitHubRateHeaders? headers) =>
        headers?.ResetUnixSeconds is null
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(headers.ResetUnixSeconds.Value);
}

/// <summary>
/// HTTP adapter for GitHub enrichment endpoints: repository detail, README (raw
/// text), topics, and languages. All lookups go through the GitHub numeric id
/// endpoint (/repositories/{id}) so renames never break enrichment. A missing
/// README (404) maps to null; other errors stay typed.
/// </summary>
public sealed class GitHubRepositoryClient(
    HttpClient http,
    IOptions<GitHubOptions> options) : IGitHubRepositoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<GitHubRepositoryDetail> GetRepositoryDetailAsync(
        long gitHubId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(gitHubId, string.Empty, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GitHubHttp.ToException(response);
        }

        var dto = await response.Content.ReadFromJsonAsync<GitHubRepositoryDto>(JsonOptions, cancellationToken)
            ?? throw new GitHubMalformedResponseException(null);
        var mapped = GitHubRepositoryMapper.TryMap(dto)
            ?? throw new GitHubMalformedResponseException(null);

        return new GitHubRepositoryDetail
        {
            GitHubId = mapped.GitHubId,
            Owner = mapped.Owner,
            Name = mapped.Name,
            FullName = mapped.FullName,
            Description = mapped.Description,
            HtmlUrl = mapped.HtmlUrl,
            DefaultBranch = mapped.DefaultBranch,
            PrimaryLanguage = mapped.PrimaryLanguage,
            Stars = mapped.Stars,
            Forks = mapped.Forks,
            OpenIssues = mapped.OpenIssues,
            IsArchived = mapped.IsArchived,
            IsFork = mapped.IsFork,
            LicenseSpdx = mapped.LicenseSpdx,
            CreatedAt = mapped.CreatedAt,
            UpdatedAt = mapped.UpdatedAt,
            PushedAt = mapped.PushedAt,
        };
    }

    public async Task<GitHubReadmeContent?> GetReadmeAsync(
        long gitHubId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(gitHubId, "/readme", cancellationToken, "application/vnd.github.raw+json");

        if ((int)response.StatusCode == 404)
        {
            return null; // Repository has no README — not an error.
        }

        if (!response.IsSuccessStatusCode)
        {
            throw GitHubHttp.ToException(response);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return new GitHubReadmeContent
        {
            RawContent = raw,
            TextContent = string.Empty, // filled by the text normalizer in the processor
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),
        };
    }

    public async Task<IReadOnlyList<string>> GetTopicsAsync(
        long gitHubId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(gitHubId, "/topics", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GitHubHttp.ToException(response);
        }

        var dto = await response.Content.ReadFromJsonAsync<GitHubTopicsDto>(JsonOptions, cancellationToken);
        return dto?.Names ?? [];
    }

    public async Task<IReadOnlyDictionary<string, long>> GetLanguagesAsync(
        long gitHubId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(gitHubId, "/languages", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw GitHubHttp.ToException(response);
        }

        var map = await response.Content.ReadFromJsonAsync<Dictionary<string, long>>(JsonOptions, cancellationToken);
        return map ?? new Dictionary<string, long>();
    }

    private async Task<HttpResponseMessage> SendAsync(
        long gitHubId,
        string suffix,
        CancellationToken cancellationToken,
        string? acceptOverride = null)
    {
        var uri = new Uri(
            options.Value.BaseUrl.TrimEnd('/') + $"/repositories/{gitHubId}{suffix}",
            UriKind.Absolute);
        return await GitHubHttp.SendAsync(http, options.Value, uri, cancellationToken, acceptOverride);
    }

    private sealed class GitHubTopicsDto
    {
        public List<string>? Names { get; set; }
    }
}

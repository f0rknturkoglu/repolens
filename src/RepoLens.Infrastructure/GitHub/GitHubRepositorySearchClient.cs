using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Discovery;

namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// HTTP adapter for GitHub repository search. Maps REST DTOs to the internal
/// <see cref="GitHubRepositorySearchResult"/> model, translates GitHub failures
/// into typed exceptions, exposes rate-limit metadata, and never leaks raw
/// GitHub error text. Items that cannot be identified (missing id/name/dates)
/// are skipped so one malformed record cannot break a whole page; a body without
/// an items array is treated as malformed.
/// </summary>
public sealed class GitHubRepositorySearchClient(
    HttpClient http,
    IOptions<GitHubOptions> options) : IGitHubRepositorySearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Bounded retry for transient GitHub 5xx on this idempotent GET; 3 attempts
    // total with short backoff. Other error classes fail fast.
    private const int MaxTransientRetries = 2;

    public async Task<GitHubRepositorySearchResult> SearchAsync(
        RepositorySearchRequest request,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(request);

        for (var attempt = 0; ; attempt++)
        {
            using var response = await SendAsync(uri, cancellationToken);
            var rateHeaders = ReadRateHeaders(response);

            if (response.IsSuccessStatusCode)
            {
                return await ParseSuccessAsync(response, rateHeaders, cancellationToken);
            }

            // 429 is always a rate limit; a 403 with no remaining quota is the
            // search API's "rate limit exhausted" shape. Other 403s are rejected.
            if ((int)response.StatusCode == 429 || IsRateLimitExhausted(response))
            {
                throw ToRateLimitException(rateHeaders);
            }

            if ((int)response.StatusCode is >= 500 and <= 599
                && attempt < MaxTransientRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * (attempt + 1)), cancellationToken);
                continue;
            }

            throw ToException(response);
        }
    }

    private static bool IsRateLimitExhausted(HttpResponseMessage response) =>
        (int)response.StatusCode == 403
        && response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues)
        && int.TryParse(remainingValues.FirstOrDefault(), out var remaining)
        && remaining == 0;

    private async Task<GitHubRepositorySearchResult> ParseSuccessAsync(
        HttpResponseMessage response,
        GitHubRateHeaders? rateHeaders,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<GitHubSearchResponseDto>(
                JsonOptions, cancellationToken);

            if (body?.Items is null)
            {
                throw new GitHubMalformedResponseException(null);
            }

            var items = new List<GitHubRepositorySearchItem>(body.Items.Count);
            foreach (var dto in body.Items)
            {
                var item = TryMapItem(dto);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return new GitHubRepositorySearchResult
            {
                Items = items,
                TotalCount = body.TotalCount,
                RateLimit = ToRateLimitInfo(rateHeaders),
            };
        }
        catch (JsonException ex)
        {
            throw new GitHubMalformedResponseException(ex);
        }
    }

    private static GitHubRepositorySearchItem? TryMapItem(GitHubRepositoryDto dto)
    {
        // Identity and dates are the only hard requirements; everything else is
        // optional GitHub data the parser must tolerate missing.
        if (dto.Id is not ( > 0 and var gitHubId)
            || string.IsNullOrWhiteSpace(dto.Name)
            || string.IsNullOrWhiteSpace(dto.FullName))
        {
            return null;
        }

        var owner = dto.Owner?.Login;
        if (string.IsNullOrWhiteSpace(owner))
        {
            owner = dto.FullName.Split('/', 2)[0];
        }

        var htmlUrl = string.IsNullOrWhiteSpace(dto.HtmlUrl)
            ? $"https://github.com/{dto.FullName}"
            : dto.HtmlUrl;

        if (!TryParseDate(dto.CreatedAt, out var createdAt)
            || !TryParseDate(dto.UpdatedAt, out var updatedAt))
        {
            return null;
        }

        TryParseDate(dto.PushedAt, out var pushedAt);
        var spdx = dto.License?.SpdxId;
        if (string.Equals(spdx, "NOASSERTION", StringComparison.OrdinalIgnoreCase))
        {
            spdx = null;
        }

        return new GitHubRepositorySearchItem
        {
            GitHubId = gitHubId,
            Owner = owner,
            Name = dto.Name,
            FullName = dto.FullName,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description,
            HtmlUrl = htmlUrl,
            DefaultBranch = string.IsNullOrWhiteSpace(dto.DefaultBranch) ? null : dto.DefaultBranch,
            PrimaryLanguage = string.IsNullOrWhiteSpace(dto.Language) ? null : dto.Language,
            Stars = dto.StargazersCount ?? 0,
            Forks = dto.ForksCount ?? 0,
            OpenIssues = dto.OpenIssuesCount ?? 0,
            IsArchived = dto.Archived ?? false,
            IsFork = dto.IsFork ?? false,
            LicenseSpdx = spdx,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            PushedAt = pushedAt,
        };
    }

    private static bool TryParseDate(string? value, out DateTimeOffset result)
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }

    private Uri BuildUri(RepositorySearchRequest request)
    {
        var query = Uri.EscapeDataString(request.Query);
        return new Uri(
            options.Value.BaseUrl.TrimEnd('/')
            + $"/search/repositories?q={query}&per_page={request.PageSize}&page={request.Page}",
            UriKind.Absolute);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);
            if (!string.IsNullOrWhiteSpace(options.Value.Token))
            {
                request.Headers.Authorization = new("Bearer", options.Value.Token);
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

    private static GitHubRateHeaders? ReadRateHeaders(HttpResponseMessage response)
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

    private static GitHubRateLimitInfo? ToRateLimitInfo(GitHubRateHeaders? headers)
    {
        if (headers is null || headers.Limit is null || headers.Remaining is null)
        {
            return null;
        }

        DateTimeOffset? resetAt = headers.ResetUnixSeconds is null
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(headers.ResetUnixSeconds.Value);

        return new GitHubRateLimitInfo(headers.Limit.Value, headers.Remaining.Value, resetAt);
    }

    private static GitHubRateLimitExceededException ToRateLimitException(GitHubRateHeaders? headers)
    {
        DateTimeOffset? resetAt = headers?.ResetUnixSeconds is null
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(headers.ResetUnixSeconds.Value);
        return new GitHubRateLimitExceededException(resetAt);
    }

    private static Exception ToException(HttpResponseMessage response) =>
        (int)response.StatusCode is >= 500 and <= 599
            ? new GitHubUpstreamErrorException((int)response.StatusCode)
            : new GitHubRequestRejectedException((int)response.StatusCode);
}

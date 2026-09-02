using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Discovery;

namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// HTTP adapter for GitHub repository search. Maps REST DTOs to the internal
/// <see cref="GitHubRepositorySearchResult"/> model, translates GitHub failures
/// into typed exceptions, exposes rate-limit metadata, and never leaks raw
/// GitHub error text. Items that cannot be identified are skipped so one
/// malformed record cannot break a whole page.
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
            using var response = await GitHubHttp.SendAsync(http, options.Value, uri, cancellationToken);
            var rateHeaders = GitHubHttp.ReadRateHeaders(response);

            if (response.IsSuccessStatusCode)
            {
                return await ParseSuccessAsync(response, rateHeaders, cancellationToken);
            }

            // 429 is always a rate limit; a 403 with no remaining quota is the
            // search API's "rate limit exhausted" shape. Other 403s are rejected.
            if ((int)response.StatusCode == 429 || GitHubHttp.IsRateLimitExhausted(response))
            {
                throw GitHubHttp.ToRateLimitException(rateHeaders);
            }

            if ((int)response.StatusCode is >= 500 and <= 599
                && attempt < MaxTransientRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * (attempt + 1)), cancellationToken);
                continue;
            }

            throw GitHubHttp.ToException(response);
        }
    }

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
                var mapped = GitHubRepositoryMapper.TryMap(dto);
                if (mapped is null)
                {
                    continue;
                }

                items.Add(new GitHubRepositorySearchItem
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
                });
            }

            return new GitHubRepositorySearchResult
            {
                Items = items,
                TotalCount = body.TotalCount,
                RateLimit = GitHubHttp.ToRateLimitInfo(rateHeaders),
            };
        }
        catch (JsonException ex)
        {
            throw new GitHubMalformedResponseException(ex);
        }
    }

    private Uri BuildUri(RepositorySearchRequest request)
    {
        var query = Uri.EscapeDataString(request.Query);
        return new Uri(
            options.Value.BaseUrl.TrimEnd('/')
            + $"/search/repositories?q={query}&per_page={request.PageSize}&page={request.Page}",
            UriKind.Absolute);
    }
}

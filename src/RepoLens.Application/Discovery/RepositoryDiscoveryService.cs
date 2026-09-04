using RepoLens.Application.Enrichment;
using RepoLens.Domain.Discovery;

namespace RepoLens.Application.Discovery;

/// <summary>
/// Use case: search GitHub for repositories, persist them idempotently (identity
/// = GitHub numeric id), auto-enqueue enrichment for repositories that have never
/// been enriched, and return the canonical stored list plus safe pagination and
/// rate-limit metadata. Pure orchestration — no EF or HTTP knowledge here.
/// </summary>
public sealed class RepositoryDiscoveryService(
    IGitHubRepositorySearchClient gitHubSearch,
    IRepositoryStore repositoryStore,
    RepositoryEnrichmentScheduler enrichmentScheduler)
{
    public const int MinPage = 1;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;
    public const int MaxQueryLength = 256;

    /// <summary>Validates user input before any upstream call. Returns null when valid.</summary>
    public static IReadOnlyList<string>? ValidateQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ["Query is required."];
        }

        if (query.Length > MaxQueryLength)
        {
            return [$"Query must be at most {MaxQueryLength} characters."];
        }

        return null;
    }

    public async Task<DiscoveryResponse> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var searchResult = await gitHubSearch.SearchAsync(
            new RepositorySearchRequest(query.Trim(), page, pageSize),
            cancellationToken);

        // Normalization: GitHub-shaped items -> discovery model. Optional fields
        // GitHub omitted stay null and map straight through.
        var discoveredAt = DateTimeOffset.UtcNow;
        var repositories = searchResult.Items
            .Select(item => new Repository.RepositoryData(
                item.GitHubId,
                item.Owner,
                item.Name,
                item.FullName,
                item.Description,
                item.HtmlUrl,
                item.DefaultBranch,
                item.PrimaryLanguage,
                item.Stars,
                item.Forks,
                item.OpenIssues,
                item.IsArchived,
                item.IsFork,
                item.LicenseSpdx,
                item.CreatedAt,
                item.UpdatedAt,
                item.PushedAt))
            .Select(data => Repository.Create(data, discoveredAt))
            .ToList();

        repositories = (await repositoryStore.UpsertAsync(repositories, cancellationToken)).ToList();

        // Never-enriched repositories get an enrichment job; refreshing already
        // enriched ones is an explicit user action.
        await enrichmentScheduler.EnqueueNewRepositoriesAsync(repositories, cancellationToken);

        return new DiscoveryResponse
        {
            Query = query.Trim(),
            Pagination = new PageMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = searchResult.TotalCount,
                ItemCount = repositories.Count,
            },
            RateLimit = searchResult.RateLimit is null
                ? null
                : new GitHubRateLimitDto(
                    searchResult.RateLimit.Limit,
                    searchResult.RateLimit.Remaining,
                    searchResult.RateLimit.ResetAt),
            Items = repositories.Select(ToDto).ToList(),
        };
    }

    private static RepositoryListItemDto ToDto(Repository repository) => new()
    {
        Id = repository.Id,
        GitHubId = repository.GitHubId,
        Owner = repository.Owner,
        Name = repository.Name,
        FullName = repository.FullName,
        Description = repository.Description,
        HtmlUrl = repository.HtmlUrl,
        DefaultBranch = repository.DefaultBranch,
        PrimaryLanguage = repository.PrimaryLanguage,
        Stars = repository.Stars,
        Forks = repository.Forks,
        OpenIssues = repository.OpenIssues,
        IsArchived = repository.IsArchived,
        IsFork = repository.IsFork,
        LicenseSpdx = repository.LicenseSpdx,
        CreatedAtUtc = repository.CreatedAt,
        UpdatedAtUtc = repository.UpdatedAt,
        PushedAtUtc = repository.PushedAt,
    };
}

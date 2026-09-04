using System.Text.Json;
using RepoLens.Application.Analysis;
using RepoLens.Application.Discovery;
using RepoLens.Application.Enrichment;
using RepoLens.Domain.Portfolio;

namespace RepoLens.Application.Portfolio;

/// <summary>
/// Portfolio analysis: fetch a user's public repositories (bounded), persist
/// them like any discovery, extract deterministic signal evidence per category,
/// compute coverage bands, and snapshot the report. Uses public metadata only —
/// no private access, no skill claims.
/// </summary>
public sealed class PortfolioAnalysisService(
    IGitHubRepositoryClient gitHub,
    IPortfolioStore portfolioStore,
    IRepositoryStore repositoryStore)
{
    public const int MaxRepositoriesFetched = 200;
    private static readonly TimeSpan CacheWindow = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static IReadOnlyList<string>? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return ["GitHub username is required."];
        }

        if (username.Length > 64 || !username.Trim().All(c =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9') || c is '-' or '_'))
        {
            return ["GitHub username contains invalid characters."];
        }

        return null;
    }

    /// <summary>Analyzes (or replays a fresh cached report for) a username.</summary>
    public async Task<PortfolioAnalysis> AnalyzeAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = NormalizeUsername(username);

        var cached = await portfolioStore.GetRecentAsync(normalized, cancellationToken);
        if (cached is not null && DateTimeOffset.UtcNow - cached.CreatedAtUtc <= CacheWindow)
        {
            return cached;
        }

        var analysis = await portfolioStore.BeginAsync(normalized, DateTimeOffset.UtcNow, cancellationToken);
        try
        {
            var details = await gitHub.GetUserRepositoriesAsync(
                normalized, MaxRepositoriesFetched, cancellationToken);

            // Persist like any discovery (idempotent by GitHub id).
            var discoveredAt = DateTimeOffset.UtcNow;
            var repositories = details
                .Select(d => RepoLens.Domain.Discovery.Repository.Create(
                    new RepoLens.Domain.Discovery.Repository.RepositoryData(
                        d.GitHubId, d.Owner, d.Name, d.FullName, d.Description, d.HtmlUrl,
                        d.DefaultBranch, d.PrimaryLanguage, d.Stars, d.Forks, d.OpenIssues,
                        d.IsArchived, d.IsFork, d.LicenseSpdx, d.CreatedAt, d.UpdatedAt, d.PushedAt),
                    discoveredAt))
                .ToList();
            repositories = (await repositoryStore.UpsertAsync(repositories, cancellationToken)).ToList();

            var repoById = repositories.ToDictionary(r => r.GitHubId);
            var portfolioRepos = details.Select(d => new PortfolioRepo(
                repoById[d.GitHubId].Id,
                d.FullName,
                d.PrimaryLanguage,
                d.Description,
                d.Stars,
                d.IsArchived,
                d.IsFork,
                SizeBytes: 0,
                d.PushedAt,
                d.Topics)).ToList();

            var coverage = PortfolioSignals.Extract(portfolioRepos);
            var analyzedCount = portfolioRepos.Count(r => !r.IsFork && !r.IsArchived);

            await portfolioStore.CompleteAsync(
                analysis.Id,
                analyzedCount,
                portfolioRepos.Count,
                JsonSerializer.Serialize(coverage.AllEvidence, JsonOptions),
                JsonSerializer.Serialize(coverage.Entries, JsonOptions),
                cancellationToken);

            return await portfolioStore.GetAsync(analysis.Id, cancellationToken)
                ?? throw new InvalidOperationException("Portfolio analysis disappeared.");
        }
        catch (Exception ex) when (ex is GitHubRateLimitExceededException
                                   or GitHubUnavailableException
                                   or GitHubUpstreamErrorException
                                   or GitHubRequestRejectedException)
        {
            await portfolioStore.FailAsync(analysis.Id, cancellationToken);
            throw;
        }
    }
}

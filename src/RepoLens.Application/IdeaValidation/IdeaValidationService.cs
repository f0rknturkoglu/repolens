using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RepoLens.Application.Ai;
using RepoLens.Application.Analysis;
using RepoLens.Application.Discovery;

namespace RepoLens.Application.IdeaValidation;

using IdeaValidationEntity = RepoLens.Domain.Analysis.IdeaValidation;

/// <summary>
/// Idea validation pipeline: idea → bounded search plan (LLM-assisted with a
/// deterministic fallback) → GitHub candidate collection → ecosystem metrics +
/// clusters → deterministic novelty + competitor evidence + gap hypotheses →
/// persisted, cacheable report. GitHub calls are skipped entirely when a recent
/// identical analysis exists (simple PostgreSQL-backed cache).
/// </summary>
public sealed class IdeaValidationService(
    IGitHubRepositorySearchClient gitHubSearch,
    IRepositoryStore repositoryStore,
    IIdeaValidationStore ideaStore,
    IAnalysisStore analysisStore,
    ILlmClient llm)
{
    public const int MaxIdeaLength = 4000;
    public const int MaxCandidatesPerQuery = 30;
    public const int MaxTotalCandidates = 90;
    public static readonly TimeSpan CacheWindow = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<string>? ValidateIdea(string? idea)
    {
        if (string.IsNullOrWhiteSpace(idea))
        {
            return ["Project idea is required."];
        }

        if (idea.Length > MaxIdeaLength)
        {
            return [$"Project idea must be at most {MaxIdeaLength} characters."];
        }

        return null;
    }

    public static string Normalize(string idea)
    {
        var collapsed = string.Join(' ', idea.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToLowerInvariant();
    }

    public static string Hash(string normalizedIdea)
    {
        var bytes = Encoding.UTF8.GetBytes(normalizedIdea);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Returns a completed validation (cached or freshly computed) with the cache flag.</summary>
    public async Task<(IdeaValidationEntity Validation, bool ServedFromCache)> ValidateAsync(string idea, CancellationToken cancellationToken)
    {
        var normalized = Normalize(idea);
        var hash = Hash(normalized);

        var cached = await ideaStore.GetRecentCompletedAsync(
            hash, DateTimeOffset.UtcNow - CacheWindow, cancellationToken);
        if (cached is not null)
        {
            return (cached, true);
        }

        var validation = await ideaStore.BeginAsync(normalized, hash, DateTimeOffset.UtcNow, cancellationToken);
        try
        {
            var plan = await SearchPlanBuilder.BuildAsync(idea, llm, cancellationToken);
            var planJson = JsonSerializer.Serialize(
                plan.Queries.Select(q => new { q.Text, q.Source }), JsonOptions);

            var candidates = await CollectCandidatesAsync(plan.Queries, cancellationToken);
            if (candidates.Count == 0)
            {
                await ideaStore.CompleteAsync(
                    validation.Id, planJson, "null", "[]", "[]",
                    JsonSerializer.Serialize(IdeaNoveltyScorer.Compute([], SearchPlanBuilder.IdeaTerms(idea), DateTimeOffset.UtcNow), JsonOptions),
                    JsonSerializer.Serialize(GapHypothesisBuilder.Build([], SearchPlanBuilder.IdeaTerms(idea)), JsonOptions),
                    cancellationToken);
                return (await ideaStore.GetAsync(validation.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Validation disappeared."), false);
            }

            var features = await analysisStore.GetRepoFeaturesAsync(
                candidates.Select(c => c.RepositoryId).ToList(), cancellationToken);

            var metrics = EcosystemMetricsCalculator.Compute(features, RepoSimilarity.Edges(features));
            var clusters = Clusterizer.Build(features, RepoSimilarity.Edges(features));
            var ideaTerms = SearchPlanBuilder.IdeaTerms(idea);
            var novelty = IdeaNoveltyScorer.Compute(features, ideaTerms, DateTimeOffset.UtcNow);
            var gaps = GapHypothesisBuilder.Build(features, ideaTerms);

            await ideaStore.CompleteAsync(
                validation.Id,
                planJson,
                EcosystemMetricsCalculator.Serialize(metrics),
                JsonSerializer.Serialize(clusters.Select(c => new
                {
                    c.Label,
                    Members = c.Members.Select(m => new { m.RepositoryId, m.Centrality }),
                }), JsonOptions),
                JsonSerializer.Serialize(novelty.Competitors, JsonOptions),
                JsonSerializer.Serialize(novelty, JsonOptions),
                JsonSerializer.Serialize(gaps, JsonOptions),
                cancellationToken);

            return (await ideaStore.GetAsync(validation.Id, cancellationToken)
                ?? throw new InvalidOperationException("Validation disappeared."), false);
        }
        catch (Exception ex) when (ex is GitHubRateLimitExceededException
                                   or GitHubUnavailableException
                                   or GitHubUpstreamErrorException
                                   or GitHubRequestRejectedException)
        {
            await ideaStore.FailAsync(validation.Id, cancellationToken);
            throw;
        }
    }

    private async Task<List<CandidateHit>> CollectCandidatesAsync(
        IReadOnlyList<PlanQuery> queries,
        CancellationToken cancellationToken)
    {
        var candidates = new List<CandidateHit>();
        var seen = new HashSet<long>();
        var discoveredAt = DateTimeOffset.UtcNow;

        foreach (var query in queries)
        {
            if (candidates.Count >= MaxTotalCandidates)
            {
                break;
            }

            var result = await gitHubSearch.SearchAsync(
                new RepositorySearchRequest(query.Text, 1, MaxCandidatesPerQuery),
                cancellationToken);

            var repositories = result.Items
                .Select(item => RepoLens.Domain.Discovery.Repository.Create(
                    new RepoLens.Domain.Discovery.Repository.RepositoryData(
                        item.GitHubId, item.Owner, item.Name, item.FullName, item.Description,
                        item.HtmlUrl, item.DefaultBranch, item.PrimaryLanguage, item.Stars, item.Forks,
                        item.OpenIssues, item.IsArchived, item.IsFork, item.LicenseSpdx,
                        item.CreatedAt, item.UpdatedAt, item.PushedAt),
                    discoveredAt))
                .ToList();
            repositories = (await repositoryStore.UpsertAsync(repositories, cancellationToken)).ToList();

            var rank = 1;
            foreach (var repository in repositories)
            {
                if (seen.Add(repository.GitHubId))
                {
                    candidates.Add(new CandidateHit(repository.Id, query.Source, rank));
                }

                rank++;
            }
        }

        return candidates;
    }
}

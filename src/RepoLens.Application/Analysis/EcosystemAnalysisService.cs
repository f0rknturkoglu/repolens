using System.Text.Json;
using RepoLens.Application.Discovery;
using RepoLens.Domain.Analysis;

namespace RepoLens.Application.Analysis;

/// <summary>A deterministic GitHub search variant derived from a user query.</summary>
public sealed record QueryVariant(string Label, string Query);

/// <summary>A candidate found by one variant (provenance for the candidate row).</summary>
public sealed record CandidateHit(long RepositoryId, string Variant, int Rank);

/// <summary>Persistence port for ecosystem analyses (sessions + snapshots).</summary>
public interface IAnalysisStore
{
    /// <summary>Creates a Running session row and returns it with its id.</summary>
    Task<EcosystemAnalysis> BeginAsync(string query, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);

    /// <summary>Completes a session: status + metrics + variants + candidate and cluster rows.</summary>
    Task CompleteAsync(
        long analysisId,
        string metricsJson,
        string variantsJson,
        IReadOnlyList<CandidateHit> candidates,
        IReadOnlyList<EcosystemCluster> clusters,
        CancellationToken cancellationToken);

    /// <summary>Marks a session failed with a safe error category.</summary>
    Task FailAsync(long analysisId, EcosystemAnalysisError error, CancellationToken cancellationToken);

    Task<EcosystemAnalysis?> GetAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EcosystemCluster>> GetClustersAsync(long analysisId, CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> GetCandidateRepositoryIdsAsync(long analysisId, CancellationToken cancellationToken);

    /// <summary>Feature rows (with topics) for the given internal repository ids.</summary>
    Task<IReadOnlyList<RepoFeatures>> GetRepoFeaturesAsync(IReadOnlyList<long> repositoryIds, CancellationToken cancellationToken);
}

/// <summary>Serialized members of a cluster: [{repositoryId, centrality}] ordered by centrality.</summary>
public sealed record ClusterMember(long RepositoryId, double Centrality);

/// <summary>
/// Orchestrates an ecosystem analysis: deterministic query variants → bounded
/// candidate collection through GitHub → similarity graph → connected-component
/// clusters → metric + evidence snapshot. Everything except the GitHub search is
/// pure and deterministic; sessions persist so results are reproducible.
/// </summary>
public sealed class EcosystemAnalysisService(
    IGitHubRepositorySearchClient gitHubSearch,
    IRepositoryStore repositoryStore,
    IAnalysisStore analysisStore)
{
    public const int MaxQueryLength = 200;
    public const int MaxCandidatesPerVariant = 30;

    public static IReadOnlyList<QueryVariant> BuildVariants(string query)
    {
        var trimmed = query.Trim();
        var tokens = trimmed
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length <= 40)
            .ToList();

        var variants = new List<QueryVariant> { new("exact", trimmed) };

        // Topic combination: topic:<term> per term, up to three terms. Matches
        // repos that tag ALL of the dominant concepts.
        var topicTerms = tokens
            .Where(t => t.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            .Take(3)
            .ToList();
        if (topicTerms.Count is >= 1 and <= 3)
        {
            variants.Add(new QueryVariant("topic", string.Join(' ', topicTerms.Select(t => $"topic:{t}"))));
        }

        // Broad scoped search: full terms across name/description/readme.
        variants.Add(new QueryVariant("broad", $"{trimmed} in:name,description,readme"));

        return variants.DistinctBy(v => v.Query).ToList();
    }

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

    /// <summary>Runs a full analysis synchronously and persists the snapshot.</summary>
    public async Task<EcosystemAnalysis> AnalyzeAsync(string query, CancellationToken cancellationToken)
    {
        var analysis = await analysisStore.BeginAsync(query.Trim(), DateTimeOffset.UtcNow, cancellationToken);

        try
        {
            var candidates = await CollectCandidatesAsync(analysis.Id, query.Trim(), cancellationToken);

            if (candidates.Count == 0)
            {
                await analysisStore.FailAsync(analysis.Id, EcosystemAnalysisError.NoCandidates, cancellationToken);
                return analysis;
            }

            var features = await analysisStore.GetRepoFeaturesAsync(
                candidates.Select(c => c.RepositoryId).ToList(), cancellationToken);
            var edges = RepoSimilarity.Edges(features);
            var metrics = EcosystemMetricsCalculator.Compute(features, edges);
            var clusters = BuildClusters(analysis.Id, features, edges);
            var variantsJson = JsonSerializer.Serialize(
                BuildVariants(query.Trim()).Select(v => new { v.Label, v.Query }));

            var metricsJson = EcosystemMetricsCalculator.Serialize(metrics);
            await analysisStore.CompleteAsync(analysis.Id, metricsJson, variantsJson, candidates, clusters, cancellationToken);
            return await analysisStore.GetAsync(analysis.Id, cancellationToken)
                ?? throw new InvalidOperationException("Analysis disappeared after completion.");
        }
        catch (Exception ex) when (ex is GitHubRateLimitExceededException)
        {
            await analysisStore.FailAsync(analysis.Id, EcosystemAnalysisError.GitHubRateLimited, cancellationToken);
            throw;
        }
        catch (Exception ex) when (ex is GitHubUnavailableException or GitHubUpstreamErrorException or GitHubRequestRejectedException)
        {
            await analysisStore.FailAsync(analysis.Id, EcosystemAnalysisError.GitHubUnavailable, cancellationToken);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await analysisStore.FailAsync(analysis.Id, EcosystemAnalysisError.Unknown, cancellationToken);
            throw;
        }
    }

    private async Task<List<CandidateHit>> CollectCandidatesAsync(
        long analysisId,
        string query,
        CancellationToken cancellationToken)
    {
        var variants = BuildVariants(query);
        var candidates = new List<CandidateHit>();
        var seen = new HashSet<long>();

        foreach (var variant in variants)
        {
            var result = await gitHubSearch.SearchAsync(
                new RepositorySearchRequest(variant.Query, 1, MaxCandidatesPerVariant),
                cancellationToken);

            var discoveredAt = DateTimeOffset.UtcNow;
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
                    candidates.Add(new CandidateHit(repository.Id, variant.Label, rank));
                }

                rank++;
            }
        }

        return candidates;
    }

    private static List<EcosystemCluster> BuildClusters(
        long analysisId,
        IReadOnlyList<RepoFeatures> features,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        var components = RepoSimilarity.ConnectedComponents(features, edges);
        var labelIndex = 0;
        var clusters = new List<EcosystemCluster>();

        foreach (var component in components)
        {
            var label = LabelFor(component, ref labelIndex);
            var centrality = component.ToDictionary(
                member => member.Id,
                member => component.Count > 1
                    ? component.Where(other => other.Id != member.Id)
                        .Average(other => RepoSimilarity.Score(member, other))
                    : 0.0);

            var members = component
                .Select(member => new ClusterMember(member.Id, Math.Round(centrality[member.Id], 4)))
                .OrderByDescending(m => m.Centrality)
                .ThenBy(m => m.RepositoryId)
                .ToList();

            clusters.Add(new EcosystemCluster(
                analysisId,
                label,
                JsonSerializer.Serialize(members),
                DateTimeOffset.UtcNow));
        }

        return clusters;
    }

    private static string LabelFor(List<RepoFeatures> component, ref int labelIndex)
    {
        if (component.Count >= 2)
        {
            var languageShare = component
                .Where(m => m.PrimaryLanguage is not null)
                .GroupBy(m => m.PrimaryLanguage!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => (Language: g.Key, Count: g.Count()))
                .FirstOrDefault();
            if (languageShare.Language is not null && languageShare.Count / (double)component.Count >= 0.5)
            {
                return $"{languageShare.Language} projects";
            }

            var topicShare = component
                .SelectMany(m => m.Topics)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => (Topic: g.Key, Count: g.Count()))
                .FirstOrDefault();
            if (topicShare.Topic is not null && topicShare.Count / (double)component.Count >= 0.4)
            {
                return $"{topicShare.Topic} ecosystem";
            }
        }

        labelIndex++;
        return component.Count == 1 ? "Standalone" : $"Cluster {labelIndex}";
    }
}

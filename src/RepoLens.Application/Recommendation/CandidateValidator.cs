using RepoLens.Application.Analysis;
using RepoLens.Application.Discovery;
using RepoLens.Application.IdeaValidation;

namespace RepoLens.Application.Recommendation;

/// <summary>GitHub landscape evidence for one candidate idea.</summary>
public sealed record CandidateLandscape(
    int CandidateCount,
    double NoveltyScore, // 0..100
    double Density,
    double LargestClusterShare,
    int ActiveCount,
    IReadOnlyList<string> Competitors); // "owner/repo (overlap 0.55)"

/// <summary>
/// Validates a candidate against the GitHub landscape with ONE bounded search
/// (title terms) — enough for evidence-backed novelty and competitor lists at a
/// rate-limit-friendly cost. Persisted via the idea-validation cache so
/// repeated recommendations reuse earlier evidence.
/// </summary>
public sealed class CandidateValidator(
    IGitHubRepositorySearchClient gitHubSearch,
    IRepositoryStore repositoryStore,
    IAnalysisStore analysisStore)
{
    public const int MaxCandidatesPerQuery = 30;

    public async Task<CandidateLandscape> ValidateAsync(
        GeneratedCandidate candidate,
        CancellationToken cancellationToken)
    {
        var query = CandidateQuery(candidate.Title);
        var result = await gitHubSearch.SearchAsync(
            new RepositorySearchRequest(query, 1, MaxCandidatesPerQuery), cancellationToken);

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
        await repositoryStore.UpsertAsync(repositories, cancellationToken);

        if (repositories.Count == 0)
        {
            return new CandidateLandscape(0, 100, 0, 0, 0, []);
        }

        var features = await analysisStore.GetRepoFeaturesAsync(
            repositories.Select(r => r.Id).ToList(), cancellationToken);
        var ideaTerms = SearchPlanBuilder.IdeaTerms($"{candidate.Title} {candidate.Summary}");
        var novelty = IdeaNoveltyScorer.Compute(features, ideaTerms, DateTimeOffset.UtcNow);
        var clusters = Clusterizer.Build(features, RepoSimilarity.Edges(features));
        var largestShare = clusters.Count > 0 ? clusters.Max(c => c.Members.Count) / (double)features.Count : 0.0;
        var active = features.Count(r => r.PushedAtUtc is { } p
            && DateTimeOffset.UtcNow - p <= TimeSpan.FromDays(90));

        return new CandidateLandscape(
            features.Count,
            novelty.Score,
            Math.Round(novelty.Components.FirstOrDefault(c => c.Key == "density")?.Value is { } d ? 1 - d : 0, 4),
            Math.Round(largestShare, 4),
            active,
            novelty.Competitors.Take(2).Select(c => $"{c.FullName} (overlap {c.Overlap:0.00})").ToList());
    }

    private static string CandidateQuery(string title)
    {
        var terms = SearchPlanBuilder.IdeaTerms(title).Take(3).ToList();
        return terms.Count > 0 ? string.Join(' ', terms) : title;
    }
}

using RepoLens.Application.Analysis;

namespace RepoLens.Application.IdeaValidation;

/// <summary>Closest-competitor evidence for the idea.</summary>
public sealed record CompetitorEvidence(
    long RepositoryId,
    string FullName,
    string? PrimaryLanguage,
    int Stars,
    bool IsArchived,
    DateTimeOffset? PushedAtUtc,
    double Overlap,
    IReadOnlyList<string> Reasons);

/// <summary>One component of the deterministic novelty score.</summary>
public sealed record NoveltyComponent(
    string Key,
    string Label,
    double Value, // 0 (saturated) .. 1 (novel)
    string Evidence,
    int WeightPercent = 0);

/// <summary>Result of the deterministic novelty calculation.</summary>
public sealed record NoveltyResult(
    double Score, // 0..100, "Estimated Novelty"
    string FormulaVersion,
    IReadOnlyList<NoveltyComponent> Components,
    IReadOnlyList<CompetitorEvidence> Competitors);

/// <summary>
/// Deterministic novelty scoring over the *analyzed candidate set only*
/// (novelty-v1). Every component is an evidence-backed number; the formula is
/// versioned and documented (docs/scoring.md). No LLM contributes scores.
/// </summary>
public static class IdeaNoveltyScorer
{
    public const int ActiveCompetitorWindowDays = 90;
    public const int CompetitorLimit = 3;
    public const double NearDuplicateThreshold = 0.7;

    public const int WeightClosestCompetitorPercent = 30;
    public const int WeightDensityPercent = 25;
    public const int WeightClusterDominancePercent = 20;
    public const int WeightActiveCompetitionPercent = 15;
    public const int WeightConceptRedundancyPercent = 10;

    public static double ClosestCompetitorOverlap(
        RepoFeatures candidate,
        IReadOnlyList<string> ideaTerms)
    {
        var candidateTerms = RepoSimilarity.NameTokens(candidate.Name)
            .Union(candidate.Topics.Select(t => t.ToLowerInvariant()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (candidateTerms.Count == 0)
        {
            return 0.0;
        }

        var overlap = candidateTerms.Intersect(ideaTerms).Count();
        return 2.0 * overlap / (candidateTerms.Count + ideaTerms.Count);
    }

    public static NoveltyResult Compute(
        IReadOnlyList<RepoFeatures> candidates,
        IReadOnlyList<string> ideaTerms,
        DateTimeOffset now)
    {
        if (candidates.Count == 0)
        {
            return new NoveltyResult(
                Score: 100,
                FormulaVersion: RepoLens.Domain.Analysis.IdeaValidation.NoveltyFormulaVersion,
                Components:
                [
                    new NoveltyComponent("candidates", "Candidate presence", 1.0,
                        "No candidate repositories were found in the analyzed set.", 100),
                ],
                Competitors: []);
        }

        var edges = RepoSimilarity.Edges(candidates);
        var components_ = Clusterizer.Build(candidates, edges);
        var largestClusterShare = components_.Count > 0
            ? components_.Max(c => c.Members.Count) / (double)candidates.Count
            : 0.0;
        var density = MeanPairwiseSimilarity(candidates);
        var activeCount = candidates.Count(r =>
            r.PushedAtUtc is { } pushed && now - pushed <= TimeSpan.FromDays(ActiveCompetitorWindowDays));
        var nearDuplicatePairShare = NearDuplicatePairShare(candidates, edges);

        var competitors = candidates
            .Select(c => new CompetitorEvidence(
                c.Id,
                c.FullName,
                c.PrimaryLanguage,
                c.Stars,
                c.IsArchived,
                c.PushedAtUtc,
                ClosestCompetitorOverlap(c, ideaTerms),
                SharedReasons(c, ideaTerms)))
            .OrderByDescending(c => c.Overlap)
            .ThenByDescending(c => c.Stars)
            .Take(CompetitorLimit)
            .ToList();

        var closest = competitors.Count > 0 ? competitors[0].Overlap : 0.0;

        var componentList = BuildComponents(
            candidates.Count, closest, density, largestClusterShare, activeCount,
            nearDuplicatePairShare, competitors);

        // novelty-v1 weights: closest-competitor distance 30%, density 25%,
        // cluster dominance 20%, active competition 15%, redundancy 10%.
        var total = Math.Round(100.0 * (
            (WeightClosestCompetitorPercent / 100.0) * componentList[0].Value
            + (WeightDensityPercent / 100.0) * componentList[1].Value
            + (WeightClusterDominancePercent / 100.0) * componentList[2].Value
            + (WeightActiveCompetitionPercent / 100.0) * componentList[3].Value
            + (WeightConceptRedundancyPercent / 100.0) * componentList[4].Value), 0);

        return new NoveltyResult(
            total,
            RepoLens.Domain.Analysis.IdeaValidation.NoveltyFormulaVersion,
            componentList,
            competitors);
    }

    private static IReadOnlyList<NoveltyComponent> BuildComponents(
        int candidateCount,
        double closest,
        double density,
        double largestClusterShare,
        int activeCount,
        double nearDuplicatePairShare,
        IReadOnlyList<CompetitorEvidence> competitors)
    {
        var nearest = competitors.Count > 0 ? competitors[0] : null;
        var nearestText = nearest is null
            ? "No overlapping repository found in the analyzed set."
            : $"Closest match: {nearest.FullName} (overlap {nearest.Overlap:0.00}).";

        var activeText = activeCount switch
        {
            0 => "No active (pushed within 90 days) competitors in the analyzed set.",
            1 => "1 active competitor in the analyzed set.",
            _ => $"{activeCount} active competitors in the analyzed set.",
        };

        return
        [
            new NoveltyComponent("competitor_gap", "Closest competitor distance", 1 - closest, nearestText, WeightClosestCompetitorPercent),
            new NoveltyComponent("density", "Similarity density", 1 - density,
                $"Mean pairwise similarity of candidates is {density:0.00}.", WeightDensityPercent),
            new NoveltyComponent("cluster_dominance", "Largest cluster share", 1 - largestClusterShare,
                $"Largest cluster holds {Math.Round(largestClusterShare * 100)}% of candidates.", WeightClusterDominancePercent),
            new NoveltyComponent("active_competition", "Active competitor scarcity",
                Math.Clamp(1 - activeCount / 20.0, 0, 1), activeText, WeightActiveCompetitionPercent),
            new NoveltyComponent("concept_redundancy", "Near-duplicate scarcity",
                1 - nearDuplicatePairShare,
                $"{Math.Round(nearDuplicatePairShare * 100)}% of candidate pairs look near-duplicate (similarity ≥ {NearDuplicateThreshold:0.00}).", WeightConceptRedundancyPercent),
        ];
    }

    private static double MeanPairwiseSimilarity(IReadOnlyList<RepoFeatures> candidates)
    {
        double total = 0;
        var pairs = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                total += RepoSimilarity.Score(candidates[i], candidates[j]);
                pairs++;
            }
        }

        return pairs > 0 ? total / pairs : 0.0;
    }

    private static double NearDuplicatePairShare(
        IReadOnlyList<RepoFeatures> candidates,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        if (candidates.Count < 2)
        {
            return 0.0;
        }

        var totalPairs = candidates.Count * (candidates.Count - 1) / 2;
        var nearDuplicates = edges.Count(e => e.Score >= NearDuplicateThreshold);
        return nearDuplicates / (double)totalPairs;
    }

    private static IReadOnlyList<string> SharedReasons(RepoFeatures candidate, IReadOnlyList<string> ideaTerms)
    {
        var reasons = new List<string>();
        var sharedTopics = candidate.Topics
            .Where(t => ideaTerms.Contains(t, StringComparer.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (sharedTopics.Count > 0)
        {
            reasons.Add($"Shares concept topic(s): {string.Join(", ", sharedTopics)}");
        }

        var sharedNameTerms = RepoSimilarity.NameTokens(candidate.Name)
            .Intersect(ideaTerms, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        if (sharedNameTerms.Count > 0)
        {
            reasons.Add($"Similar name term(s): {string.Join(", ", sharedNameTerms)}");
        }

        return reasons;
    }
}

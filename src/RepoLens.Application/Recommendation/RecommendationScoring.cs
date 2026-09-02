using RepoLens.Application.Portfolio;

namespace RepoLens.Application.Recommendation;

/// <summary>Scope/label of a candidate from its own text (deterministic).</summary>
public sealed class Feasibility
{
    public required string Scope { get; init; } // Small | Medium | Large | Very Large
    public required double Score { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}

public static class CandidateFeasibility
{
    // Words that suggest non-trivial integrations or infrastructure.
    private static readonly string[] IntegrationSignals =
    ["api", "oauth", "webhook", "sdk", "integration", "plugin", "sync", "protocol"];
    private static readonly string[] InfrastructureSignals =
    ["kafka", "postgres", "database", "kubernetes", "docker", "queue", "cluster", "distributed", "streaming", "redis", "rag", "embedding", "llm", "model"];

    public static Feasibility Assess(string candidateText, string? durationWeeks)
    {
        var text = candidateText.ToLowerInvariant();
        var integrations = IntegrationSignals.Count(text.Contains);
        var infrastructure = InfrastructureSignals.Count(text.Contains);
        var signals = integrations + infrastructure;

        var scope = signals switch
        {
            <= 1 => "Small",
            <= 3 => "Medium",
            <= 5 => "Large",
            _ => "Very Large",
        };

        var reasons = new List<string>();
        if (integrations > 0)
        {
            reasons.Add($"{integrations} external integration point(s) implied by the description.");
        }

        if (infrastructure > 0)
        {
            reasons.Add($"{infrastructure} infrastructure/data requirements implied (e.g. queue, database, model serving).");
        }

        if (int.TryParse(durationWeeks, out var weeks) && weeks > 0)
        {
            reasons.Add($"Duration constraint: {weeks} week{(weeks == 1 ? "" : "s")}.");
        }

        var score = scope switch
        {
            "Small" => 0.9,
            "Medium" => 0.7,
            "Large" => 0.45,
            _ => 0.25,
        };

        // A tight window against a big scope drags feasibility down.
        if (int.TryParse(durationWeeks, out var w) && w is > 0 and < 6 && scope is "Large" or "Very Large")
        {
            score -= 0.15;
        }

        reasons.Add($"Relative scope estimate: {scope}.");
        return new Feasibility { Scope = scope, Score = Math.Clamp(score, 0.1, 1), Reasons = reasons };
    }
}

/// <summary>
/// Deterministic final scoring (rec-v1). Weights are documented in
/// docs/scoring.md; an LLM never overrides this ranking.
/// </summary>
public static class RecommendationScorer
{
    public static double PortfolioMarginalNeutral = 0.5;

    public static double Score(
        double originality, // 0..1 (novelty/100)
        double portfolioMarginal, // 0..1 (or neutral when no portfolio)
        double feasibility, // 0..1
        double goalAlignment, // 0..1
        double interestAlignment) // 0..1
        =>
        Math.Round(
            0.30 * originality
            + 0.20 * portfolioMarginal
            + 0.20 * feasibility
            + 0.15 * goalAlignment
            + 0.15 * interestAlignment,
            4);

    /// <summary>Overlap between free-text (goal/interests) and candidate category keywords.</summary>
    public static double Alignment(string freeText, string category)
    {
        var text = freeText.ToLowerInvariant();
        var categoryDef = PortfolioTaxonomy.All.FirstOrDefault(c =>
            string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));
        if (categoryDef is null)
        {
            return 0.5;
        }

        var hits = categoryDef.Keywords.Count(text.Contains);
        return hits > 0 ? 0.7 + Math.Min(0.3, hits * 0.05) : 0.25;
    }
}

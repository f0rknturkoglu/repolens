using RepoLens.Application.Analysis;

namespace RepoLens.Application.IdeaValidation;

/// <summary>A gap hypothesis — scoped explicitly to the analyzed candidate set.</summary>
public sealed record GapHypothesis(string Kind, string Statement);

/// <summary>
/// Produces deterministic, candidate-scoped gap hypotheses from the analyzed
/// set. Statements never claim global GitHub absence — they describe what was
/// and was not observed within the collected candidates ("Static migration
/// linting appears common; production-like load simulation appears less
/// represented in the analyzed candidate set.").
/// </summary>
public static class GapHypothesisBuilder
{
    public const double SaturatedThreshold = 0.35;
    public const double GapThreshold = 0.15;

    public static IReadOnlyList<GapHypothesis> Build(
        IReadOnlyList<RepoFeatures> candidates,
        IReadOnlyList<string> ideaTerms)
    {
        var hypotheses = new List<GapHypothesis>();
        if (candidates.Count == 0)
        {
            hypotheses.Add(new GapHypothesis("gap",
                "No candidate repositories were found, so no observation-based gap statements are possible."));
            return hypotheses;
        }

        // Saturation statements for concepts the idea shares with most candidates.
        var topicCounts = candidates
            .SelectMany(c => c.Topics)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var term in ideaTerms)
        {
            var present = topicCounts.GetValueOrDefault(term, 0);
            var share = present / (double)candidates.Count;
            if (share >= SaturatedThreshold)
            {
                hypotheses.Add(new GapHypothesis("saturated",
                    $"Repositories tagged '{term}' appear common: {present} of {candidates.Count} candidates carry the tag."));
            }
            else if (share < GapThreshold && term.Length >= 4)
            {
                hypotheses.Add(new GapHypothesis("gap",
                    $"Repositories tagged '{term}' appear less represented: {present} of {candidates.Count} candidates carry the tag."));
            }
        }

        if (hypotheses.Count == 0)
        {
            hypotheses.Add(new GapHypothesis("observation",
                $"No strong saturation or gap signal was found for the idea's terms across {candidates.Count} candidates."));
        }

        return hypotheses;
    }
}

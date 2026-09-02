using RepoLens.Application.Analysis;

namespace RepoLens.Application.Portfolio;

/// <summary>A repository considered in portfolio analysis.</summary>
public sealed record PortfolioRepo(
    long RepositoryId,
    string FullName,
    string? Language,
    string? Description,
    int Stars,
    bool IsArchived,
    bool IsFork,
    long SizeBytes,
    DateTimeOffset? PushedAtUtc,
    IReadOnlyList<string> Topics);

/// <summary>One evidence item: a repository contributed to a category with a reason.</summary>
public sealed record SignalEvidence(string Category, string RepositoryFullName, string Reason, int Stars);

public sealed record CoverageEntry(
    string Category,
    int EvidenceCount,
    IReadOnlyList<string> RepositoryFullNames,
    string Band); // strong | moderate | limited

public sealed record PortfolioCoverage(
    IReadOnlyList<CoverageEntry> Entries,
    IReadOnlyList<SignalEvidence> AllEvidence);

public sealed record CategoryMarginalValue(string Category, string CurrentBand, double Value, string Rationale);

public sealed record MarginalValueResult(
    double Score, // 0..1
    string FormulaVersion,
    IReadOnlyList<CategoryMarginalValue> Categories);

/// <summary>
/// Deterministic signal extraction from repository metadata (language,
/// description, topics, name). One evidence item per repository per category
/// (deduplicated), each tied to a concrete reason — no skill claims, only
/// portfolio signals (docs/product.md).
/// </summary>
public static class PortfolioSignals
{
    /// <summary>Signal extraction from public repo metadata alone (no enrichment needed).</summary>
    public static PortfolioCoverage Extract(IReadOnlyList<PortfolioRepo> repos)
    {
        var evidence = new List<SignalEvidence>();
        foreach (var repo in repos)
        {
            foreach (var category in PortfolioTaxonomy.All)
            {
                var reasons = new List<string>();
                if (category.MatchesLanguage(repo.Language))
                {
                    reasons.Add($"Primary language: {repo.Language}");
                }

                var text = string.Join(' ', repo.Description ?? string.Empty, string.Join(' ', repo.Topics), repo.FullName)
                    .ToLowerInvariant();
                var matchedKeywords = category.Keywords
                    .Where(text.Contains)
                    .Take(1)
                    .ToList();
                if (matchedKeywords.Count > 0)
                {
                    reasons.Add($"Describes itself as {matchedKeywords[0]}-related");
                }

                if (reasons.Count > 0)
                {
                    evidence.Add(new SignalEvidence(category.Name, repo.FullName, reasons[0], repo.Stars));
                }
            }
        }

        var entries = PortfolioTaxonomy.All.Select(category =>
        {
            var categoryEvidence = evidence.Where(e => e.Category == category.Name).ToList();
            var reposInCategory = categoryEvidence.Select(e => e.RepositoryFullName).Distinct().ToList();
            return new CoverageEntry(category.Name, categoryEvidence.Count, reposInCategory, Band(reposInCategory.Count));
        }).ToList();

        return new PortfolioCoverage(entries, evidence);
    }

    public static string Band(int repositoryCount) => repositoryCount switch
    {
        >= 3 => "strong",
        >= 1 => "moderate",
        _ => "limited",
    };

    /// <summary>Marginal value of a candidate idea/category against current coverage.</summary>
    public static MarginalValueResult MarginalValue(PortfolioCoverage coverage, string idea)
    {
        var byCategory = coverage.Entries.ToDictionary(e => e.Category, StringComparer.OrdinalIgnoreCase);
        var categories = PortfolioTaxonomy.All
            .Where(c => PortfolioTaxonomy.IdeaMatchesCategory(idea, c.Name))
            .ToList();

        // No direct category keyword match → use the best-matching category by
        // keyword overlap so the analysis never returns nothing.
        if (categories.Count == 0)
        {
            var scored = PortfolioTaxonomy.All
                .Select(c => (Category: c, Hits: c.Keywords.Count(k => idea.Contains(k, StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(x => x.Hits)
                .ToList();
            var best = scored.FirstOrDefault(x => x.Hits > 0).Category;
            if (best is not null)
            {
                categories = [best];
            }
        }

        var results = categories
            .Select(category =>
            {
                var entry = byCategory.GetValueOrDefault(category.Name)
                    ?? new CoverageEntry(category.Name, 0, [], Band(0));
                var repositoryCount = entry.RepositoryFullNames.Count;
                var coverageRatio = Math.Min(repositoryCount / 3.0, 1.0);
                var value = Math.Round(1 - coverageRatio + (repositoryCount == 0 ? 0.10 : 0.0), 2);
                return new CategoryMarginalValue(
                    category.Name,
                    entry.Band,
                    Math.Clamp(value, 0, 1),
                    repositoryCount == 0
                        ? $"No repositories currently evidence {category.Name}."
                        : $"{repositoryCount} repositor{(repositoryCount == 1 ? "y" : "ies")} currently evidence {category.Name}.");
            })
            .ToList();

        var score = results.Count > 0 ? Math.Round(results.Average(r => r.Value), 2) : 0.5;
        return new MarginalValueResult(score, PortfolioAnalysisVersion(), results);
    }

    private static string PortfolioAnalysisVersion() =>
        RepoLens.Domain.Portfolio.PortfolioAnalysis.MarginalValueFormulaVersion;
}

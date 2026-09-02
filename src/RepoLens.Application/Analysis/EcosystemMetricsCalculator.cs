using System.Text.Json;

namespace RepoLens.Application.Analysis;

/// <summary>Serializable metrics snapshot stored with each analysis.</summary>
public sealed class EcosystemMetrics
{
    public required int CandidateCount { get; init; }
    public required int ClusterCount { get; init; }
    /// <summary>Largest cluster size / candidate count (0 when no candidates).</summary>
    public required double LargestClusterShare { get; init; }
    /// <summary>Fraction of candidates that are archived (0–1).</summary>
    public required double ArchivedRatio { get; init; }
    /// <summary>Fraction pushed within the last 90 days (0–1).</summary>
    public required double RecentlyActiveRatio { get; init; }
    public required IReadOnlyList<AgeBucket> AgeDistribution { get; init; }
    public required double StarsMedian { get; init; }
    /// <summary>Share of total stars held by the top-5 repositories (0–1).</summary>
    public required double StarsConcentration { get; init; }
    public required double ForkRatio { get; init; }
    /// <summary>Mean pairwise similarity over all candidate pairs (0–1).</summary>
    public required double SimilarityDensity { get; init; }
    public required IReadOnlyList<CountShare> PrimaryLanguages { get; init; }
    public required IReadOnlyList<CountShare> Topics { get; init; }
    /// <summary>Evidence sentences every metric derives from (numbers computed here).</summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    public sealed record AgeBucket(string Label, int Count);
    public sealed record CountShare(string Value, int Count, double Share);
}

/// <summary>
/// Computes ecosystem metrics + evidence deterministically from candidate
/// features and their pairwise similarity. Every number in the report comes from
/// these inputs — there is no LLM and no second source of truth.
/// </summary>
public static class EcosystemMetricsCalculator
{
    public const int RecentlyActiveDays = 90;
    public const int ConcentrationTopN = 5;

    public static EcosystemMetrics Compute(
        IReadOnlyList<RepoFeatures> repos,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        var evidence = new List<string>();
        var now = DateTimeOffset.UtcNow;

        var candidateCount = repos.Count;
        var clusterCount = RepoSimilarity.ConnectedComponents(repos, edges).Count;

        var archivedRatio = SafeRatio(repos, r => r.IsArchived);
        var recentlyActiveRatio = SafeRatio(repos, r =>
            r.PushedAtUtc is { } pushed && now - pushed <= TimeSpan.FromDays(RecentlyActiveDays));
        var forkRatio = SafeRatio(repos, r => r.IsFork);

        var ages = repos
            .Select(r => now.Year - r.CreatedAtUtc.Year)
            .ToList();
        var ageDistribution = new[]
        {
            ("< 1 year", ages.Count(a => a < 1)),
            ("1–3 years", ages.Count(a => a is >= 1 and < 3)),
            ("3–5 years", ages.Count(a => a is >= 3 and < 5)),
            ("5–10 years", ages.Count(a => a is >= 5 and < 10)),
            ("10+ years", ages.Count(a => a >= 10)),
        }.Select(b => new EcosystemMetrics.AgeBucket(b.Item1, b.Item2)).ToList();

        var stars = repos.Select(r => (double)r.Stars).OrderBy(s => s).ToList();
        var starsMedian = stars.Count > 0
            ? stars.Count % 2 == 1
                ? stars[stars.Count / 2]
                : (stars[stars.Count / 2 - 1] + stars[stars.Count / 2]) / 2.0
            : 0;

        var totalStars = repos.Sum(r => (long)r.Stars);
        var topStars = repos.OrderByDescending(r => r.Stars).Take(ConcentrationTopN).Sum(r => (long)r.Stars);
        var starsConcentration = totalStars > 0 ? topStars / (double)totalStars : 0.0;

        var languages = repos
            .Where(r => r.PrimaryLanguage is not null)
            .GroupBy(r => r.PrimaryLanguage!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EcosystemMetrics.CountShare(g.Key, g.Count(), g.Count() / (double)Math.Max(repos.Count, 1)))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Value, StringComparer.Ordinal)
            .ToList();

        var topics = repos
            .SelectMany(r => r.Topics)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EcosystemMetrics.CountShare(g.Key, g.Count(), g.Count() / (double)Math.Max(repos.Count, 1)))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Value, StringComparer.Ordinal)
            .Take(15)
            .ToList();

        var density = MeanPairwiseSimilarity(repos);

        // Evidence sentences — every derived claim is anchored in a computed number.
        evidence.Add($"{candidateCount} candidate repositories analyzed across {clusterCount} cluster(s).");
        if (candidateCount > 0)
        {
            evidence.Add($"{Math.Round(archivedRatio * 100)}% of candidates are archived.");
            evidence.Add($"{Math.Round(recentlyActiveRatio * 100)}% of candidates were pushed in the last {RecentlyActiveDays} days.");
            evidence.Add($"Median stars: {starsMedian:0}.");
            if (totalStars > 0)
            {
                var topList = string.Join(", ", repos.OrderByDescending(r => r.Stars).Take(ConcentrationTopN).Select(r => r.FullName));
                evidence.Add($"Top {Math.Min(ConcentrationTopN, repos.Count)} repositories contain {Math.Round(starsConcentration * 100)}% of observed stars ({totalStars} total): {topList}.");
            }
        }

        return new EcosystemMetrics
        {
            CandidateCount = candidateCount,
            ClusterCount = clusterCount,
            LargestClusterShare = LargestClusterShare(repos, edges),
            ArchivedRatio = Math.Round(archivedRatio, 4),
            RecentlyActiveRatio = Math.Round(recentlyActiveRatio, 4),
            AgeDistribution = ageDistribution,
            StarsMedian = starsMedian,
            StarsConcentration = Math.Round(starsConcentration, 4),
            ForkRatio = Math.Round(forkRatio, 4),
            SimilarityDensity = Math.Round(density, 4),
            PrimaryLanguages = languages,
            Topics = topics,
            Evidence = evidence,
        };
    }

    private static double LargestClusterShare(
        IReadOnlyList<RepoFeatures> repos,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        var components = RepoSimilarity.ConnectedComponents(repos, edges);
        var largest = components.Count > 0 ? components.Max(c => c.Count) : 0;
        return repos.Count > 0 ? largest / (double)repos.Count : 0.0;
    }

    private static double SafeRatio(IReadOnlyList<RepoFeatures> repos, Func<RepoFeatures, bool> predicate) =>
        repos.Count == 0 ? 0.0 : (double)repos.Count(r => predicate(r)) / repos.Count;

    private static double MeanPairwiseSimilarity(IReadOnlyList<RepoFeatures> repos)
    {
        if (repos.Count < 2)
        {
            return 0.0;
        }

        double total = 0;
        var pairs = 0;
        for (var i = 0; i < repos.Count; i++)
        {
            for (var j = i + 1; j < repos.Count; j++)
            {
                total += RepoSimilarity.Score(repos[i], repos[j]);
                pairs++;
            }
        }

        return pairs > 0 ? total / pairs : 0.0;
    }

    public static string Serialize(EcosystemMetrics metrics) =>
        JsonSerializer.Serialize(metrics, JsonOptions);

    public static EcosystemMetrics Deserialize(string json) =>
        JsonSerializer.Deserialize<EcosystemMetrics>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored metrics are unreadable.");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

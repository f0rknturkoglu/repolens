using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RepoLens.Application.Ai;
using RepoLens.Application.Discovery;
using RepoLens.Application.Portfolio;
using RepoLens.Domain.Recommendation;

namespace RepoLens.Application.Recommendation;

/// <summary>One recommended project with evidence + explainability.</summary>
public sealed class RecommendationItem
{
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string Problem { get; init; }
    public required string TargetUser { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyList<string> WhyRankedHere { get; init; }
    public required EvidenceBlock Evidence { get; init; }
    public required string DifferentiationOpportunity { get; init; }
    public required string MvpSuggestion { get; init; }

    public sealed class EvidenceBlock
    {
        public required double Originality { get; init; }
        public required double PortfolioMarginalValue { get; init; }
        public required FeasibilityJson Feasibility { get; init; }
        public required double GoalAlignment { get; init; }
        public required double InterestAlignment { get; init; }
        public required CandidateLandscapeJson Landscape { get; init; }

        public sealed class FeasibilityJson
        {
            public required string Scope { get; init; }
            public required double Score { get; init; }
            public required IReadOnlyList<string> Reasons { get; init; }
        }

        public sealed class CandidateLandscapeJson
        {
            public required int CandidateCount { get; init; }
            public required double NoveltyScore { get; init; }
            public required double Density { get; init; }
            public required double LargestClusterShare { get; init; }
            public required int ActiveCount { get; init; }
            public required IReadOnlyList<string> Competitors { get; init; }
        }
    }
}

/// <summary>API response for a recommendation run.</summary>
public sealed class RecommendationResponse
{
    public required long Id { get; init; }
    public required string Version { get; init; }
    public required string Status { get; init; }
    public required string Goal { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<string> Interests { get; init; }
    public required string? DurationWeeks { get; init; }
    public required bool ServedFromCache { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required IReadOnlyList<RecommendationItem> Items { get; init; }
    public required string Limitations { get; init; }
}

/// <summary>Persistence port for recommendation requests.</summary>
public interface IRecommendationStore
{
    Task<RecommendationRequest> BeginAsync(
        string goal,
        string requestHash,
        string? username,
        string interestsJson,
        string constraintsJson,
        CancellationToken cancellationToken);

    Task CompleteAsync(long id, string resultJson, CancellationToken cancellationToken);

    Task FailAsync(long id, CancellationToken cancellationToken);

    Task<RecommendationRequest?> GetRecentCompletedAsync(
        string requestHash,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken);

    Task<RecommendationRequest?> GetAsync(long id, CancellationToken cancellationToken);
}

/// <summary>
/// Personalized recommendation pipeline: input → candidate generation (LLM with
/// deterministic fallback) → per-candidate GitHub validation → feasibility +
/// alignment + portfolio marginal value → deterministic rec-v1 score → category
/// diversity → explainable top 3. Cached by normalized request hash.
/// </summary>
public sealed class RecommendationService(
    CandidateValidator validator,
    IRecommendationStore store,
    PortfolioAnalysisService portfolioService,
    ILlmClient llm)
{
    public const string LimitationText =
        "Recommendations are generated and ranked deterministically from GitHub evidence collected at analysis time; LLM output is limited to candidate brainstorming and never overrides scores.";

    public static readonly TimeSpan CacheWindow = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<string>? Validate(RecommendationInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Goal))
        {
            return ["Goal is required."];
        }

        if (input.Goal.Length > 2000)
        {
            return ["Goal must be at most 2000 characters."];
        }

        if (input.Interests.Any(i => string.IsNullOrWhiteSpace(i) || i.Length > 200))
        {
            return ["Interests must be non-empty and short."];
        }

        if (input.Username is not null)
        {
            var errors = PortfolioAnalysisService.ValidateUsername(input.Username);
            if (errors is not null)
            {
                return errors;
            }
        }

        return null;
    }

    public static string RequestHash(RecommendationInput input)
    {
        var canonical = string.Join('|',
            input.Goal.Trim().ToLowerInvariant(),
            string.Join(',', input.Interests.Select(i => i.Trim().ToLowerInvariant()).OrderBy(x => x)),
            input.Username?.Trim().ToLowerInvariant() ?? string.Empty,
            input.DurationWeeks?.Trim().ToLowerInvariant() ?? string.Empty);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public async Task<RecommendationResponse> RecommendAsync(
        RecommendationInput input,
        CancellationToken cancellationToken)
    {
        var hash = RequestHash(input);
        var cached = await store.GetRecentCompletedAsync(hash, DateTimeOffset.UtcNow - CacheWindow, cancellationToken);
        if (cached is not null)
        {
            return ToResponse(cached, DeserializeItems(cached.ResultJson!), servedFromCache: true);
        }

        var request = await store.BeginAsync(
            input.Goal.Trim(),
            hash,
            input.Username is { } name ? PortfolioAnalysisService.NormalizeUsername(name) : null,
            JsonSerializer.Serialize(input.Interests),
            JsonSerializer.Serialize(new { input.DurationWeeks }),
            cancellationToken);

        try
        {
            var candidates = (await CandidateGenerator.GenerateAsync(input, llm, cancellationToken))
                .GroupBy(c => c.Title.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(CandidateGenerator.MaxCandidates)
                .ToList();

            // Optional portfolio snapshot for marginal value.
            PortfolioCoverage? portfolio = null;
            if (input.Username is not null)
            {
                var analysis = await portfolioService.AnalyzeAsync(input.Username, cancellationToken);
                portfolio = CoverageFrom(analysis);
            }

            var goalText = $"{input.Goal} {string.Join(' ', input.Interests)}";
            var scored = new List<RecommendationItem>();
            var saturatedCount = 0;

            foreach (var candidate in candidates)
            {
                var landscape = await validator.ValidateAsync(candidate, cancellationToken);
                var originality = Math.Clamp(landscape.NoveltyScore / 100.0, 0, 1);
                var keepDespiteSaturation = scored.Count + saturatedCount >= candidates.Count - 1;

                // Saturated-candidate rejection: drop hopelessly crowded ideas,
                // unless dropping them would leave no recommendations at all.
                if (landscape.NoveltyScore < 10 && !keepDespiteSaturation)
                {
                    saturatedCount++;
                    continue;
                }

                var candidateText = $"{candidate.Title} {candidate.Summary}";
                var feasibility = CandidateFeasibility.Assess(candidateText, input.DurationWeeks);
                var portfolioMarginal = portfolio is null
                    ? RecommendationScorer.PortfolioMarginalNeutral
                    : PortfolioSignals.MarginalValue(portfolio, candidateText).Score;
                var goalAlignment = RecommendationScorer.Alignment(goalText, candidate.Category);
                var interestAlignment = input.Interests.Count == 0
                    ? 0.25
                    : Math.Max(0.25, input.Interests.Max(i => RecommendationScorer.Alignment(i, candidate.Category)));
                var score = RecommendationScorer.Score(
                    originality, portfolioMarginal, feasibility.Score, goalAlignment, interestAlignment);

                scored.Add(new RecommendationItem
                {
                    Title = candidate.Title,
                    Category = candidate.Category,
                    Summary = candidate.Summary,
                    Problem = candidate.Problem,
                    TargetUser = candidate.TargetUser,
                    Score = score,
                    WhyRankedHere = BuildWhy(
                        originality, portfolioMarginal, feasibility, goalAlignment, interestAlignment),
                    Evidence = new RecommendationItem.EvidenceBlock
                    {
                        Originality = Math.Round(originality, 3),
                        PortfolioMarginalValue = Math.Round(portfolioMarginal, 3),
                        Feasibility = new RecommendationItem.EvidenceBlock.FeasibilityJson
                        {
                            Scope = feasibility.Scope,
                            Score = Math.Round(feasibility.Score, 3),
                            Reasons = feasibility.Reasons,
                        },
                        GoalAlignment = Math.Round(goalAlignment, 3),
                        InterestAlignment = Math.Round(interestAlignment, 3),
                        Landscape = new RecommendationItem.EvidenceBlock.CandidateLandscapeJson
                        {
                            CandidateCount = landscape.CandidateCount,
                            NoveltyScore = Math.Round(landscape.NoveltyScore, 1),
                            Density = landscape.Density,
                            LargestClusterShare = landscape.LargestClusterShare,
                            ActiveCount = landscape.ActiveCount,
                            Competitors = landscape.Competitors,
                        },
                    },
                    DifferentiationOpportunity = Differentiate(landscape),
                    MvpSuggestion = Mvp(feasibility),
                });
            }

            var ranked = SelectDiverse(scored);
            await store.CompleteAsync(request.Id, JsonSerializer.Serialize(ranked, JsonOptions), cancellationToken);
            return ToResponse(request, ranked, servedFromCache: false);
        }
        catch (Exception ex) when (ex is GitHubRateLimitExceededException
                                   or GitHubUnavailableException
                                   or GitHubUpstreamErrorException
                                   or GitHubRequestRejectedException)
        {
            await store.FailAsync(request.Id, cancellationToken);
            throw;
        }
    }

    /// <summary>Top-3 with at most one item per category; deterministic.</summary>
    public static List<RecommendationItem> SelectDiverse(IReadOnlyList<RecommendationItem> items)
    {
        var ordered = items.OrderByDescending(i => i.Score).ThenBy(i => i.Title, StringComparer.Ordinal).ToList();
        var chosen = new List<RecommendationItem>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ordered)
        {
            if (chosen.Count >= 3)
            {
                break;
            }

            if (categories.Add(item.Category))
            {
                chosen.Add(item);
            }
        }

        return chosen;
    }

    private static PortfolioCoverage? CoverageFrom(RepoLens.Domain.Portfolio.PortfolioAnalysis analysis)
    {
        if (analysis.CoverageJson is null)
        {
            return null;
        }

        var entries = JsonSerializer.Deserialize<List<PortfolioResponse.CoverageEntryDto>>(
            analysis.CoverageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return new PortfolioCoverage(
            entries.Select(e => new CoverageEntry(e.Category, e.EvidenceCount, e.Repositories, e.Band)).ToList(),
            []);
    }

    private static IReadOnlyList<string> BuildWhy(
        double originality,
        double portfolioMarginal,
        Feasibility feasibility,
        double goalAlignment,
        double interestAlignment)
    {
        var lines = new List<string>();
        if (originality >= 0.5)
        {
            lines.Add($"+ Estimated novelty {Math.Round(originality * 100)}/100 in the analyzed landscape.");
        }

        if (portfolioMarginal >= 0.6)
        {
            lines.Add($"+ High portfolio marginal value ({Math.Round(portfolioMarginal * 100)}%).");
        }

        if (feasibility.Score >= 0.7)
        {
            lines.Add($"+ Feasible scope: {feasibility.Scope}.");
        }

        if (goalAlignment >= 0.7)
        {
            lines.Add("+ Strong goal alignment.");
        }

        if (interestAlignment >= 0.7)
        {
            lines.Add("+ Strong interest alignment.");
        }

        if (originality < 0.35)
        {
            lines.Add("- The analyzed landscape for this idea looks crowded.");
        }

        if (feasibility.Score < 0.5)
        {
            lines.Add($"- {feasibility.Scope} scope requires real infrastructure work.");
        }

        if (lines.Count == 0)
        {
            lines.Add("Balanced profile across feasibility, originality, and fit.");
        }

        return lines;
    }

    private static string Differentiate(CandidateLandscape landscape) =>
        landscape.CandidateCount == 0
            ? "No overlapping projects were found in the analyzed candidate set — the space reads open."
            : landscape.ActiveCount == 0
                ? "Most overlapping candidates look inactive; a maintained alternative stands out."
                : landscape.Density > 0.5
                    ? "Existing projects cluster tightly; an angle that differs on the dominant approach may stand out."
                    : "The analyzed set is spread across approaches — clarity of one niche is the differentiation.";

    private static string Mvp(Feasibility feasibility) =>
        feasibility.Scope switch
        {
            "Small" => "Ship the core loop as a single-file CLI/demo end-to-end, publish it, and iterate from feedback.",
            "Medium" => "Build one vertical slice of the main workflow first (storage + one integration), then broaden.",
            _ => "Prototype the riskiest integration early (data/infra signal), then add the secondary features around a working core.",
        };

    public static RecommendationResponse FromStored(RecommendationRequest request, bool servedFromCache)
    {
        var items = request.ResultJson is null ? [] : DeserializeItems(request.ResultJson);
        return ToResponse(request, items, servedFromCache);
    }

    private static RecommendationResponse ToResponse(
        RecommendationRequest request,
        IReadOnlyList<RecommendationItem> items,
        bool servedFromCache)
    {
        var interests = request.InterestsJson is null
            ? []
            : JsonSerializer.Deserialize<List<string>>(request.InterestsJson) ?? [];
        var duration = request.ConstraintsJson is null
            ? null
            : JsonSerializer.Deserialize<ConstraintsDto>(request.ConstraintsJson)?.DurationWeeks;

        return new RecommendationResponse
        {
            Id = request.Id,
            Version = request.Version,
            Status = request.Status.ToString().ToLowerInvariant(),
            Goal = request.Goal,
            Username = request.Username,
            Interests = interests,
            DurationWeeks = duration,
            ServedFromCache = servedFromCache,
            CreatedAtUtc = request.CreatedAtUtc,
            Items = items,
            Limitations = LimitationText,
        };
    }

    private static List<RecommendationItem> DeserializeItems(string json) =>
        JsonSerializer.Deserialize<List<RecommendationItem>>(json, JsonOptions) ?? [];

    private sealed class ConstraintsDto
    {
        public string? DurationWeeks { get; set; }
    }
}

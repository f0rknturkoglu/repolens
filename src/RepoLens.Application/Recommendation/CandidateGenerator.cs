using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RepoLens.Application.Ai;
using RepoLens.Application.Portfolio;

namespace RepoLens.Application.Recommendation;

/// <summary>User input for a recommendation request (structured).</summary>
public sealed record RecommendationInput(
    string Goal,
    IReadOnlyList<string> Interests,
    string? Username,
    string? DurationWeeks);

public sealed record RecommendationConstraints(string? DurationWeeks);

public sealed record GeneratedCandidate(
    string Title,
    string Summary,
    string Problem,
    string TargetUser,
    string Category); // dominant portfolio-taxonomy-like category, for diversity

public sealed record PlanQueryLike(string Text, string Source);

/// <summary>
/// Candidate project idea generation. The deterministic fallback bank (one per
/// category) guarantees output without an LLM; LLM output is validated, cleaned
/// (no qualifiers/injection shapes), deduplicated, and capped.
/// </summary>
public static class CandidateGenerator
{
    public const int MaxCandidates = 5;

    private static readonly (string Category, string Title, string Summary, string Problem, string Target)[] FallbackBank =
    [
        ("Databases", "Schema migration replay laboratory",
            "A load-testing harness that replays real schema-change workloads against PostgreSQL staging environments.",
            "Database migrations look safe until they lock production tables under real traffic; existing tools rarely simulate production-like load.",
            "Backend developers owning database change processes"),
        ("Developer Tooling", "Git-workflow guard",
            "A CLI that enforces team conventions on branch names, commit messages, and PR descriptions from a config file.",
            "Teams reinvent git hooks per repository and enforcement drifts.",
            "Engineering teams standardizing contribution flow"),
        ("Testing", "API contract drift detector",
            "Continuously diffs documented OpenAPI contracts against live server behavior and flags breaking changes.",
            "Breaking API changes ship silently when docs and implementation drift apart.",
            "API platform teams"),
        ("AI/ML", "Local document chat over your notes",
            "Private RAG chat over a personal notes folder using local embeddings and no cloud calls.",
            "Users want retrieval over personal documents without sending them to hosted services.",
            "Privacy-conscious knowledge workers"),
        ("Observability", "Intermittent failure replayer",
            "Records flaky-test and error traces and replays them deterministically to reproduce rare failures.",
            "Intermittent failures are nearly impossible to debug from logs alone.",
            "SREs and test-owners"),
        ("Data Engineering", "Pipeline lineage walker",
            "Walks dbt/Airflow DAG definitions and prints column-level lineage with impact of upstream changes.",
            "Data teams cannot see what breaks when a source column changes.",
            "Analytics engineers"),
        ("DevOps", "Environments-as-code lint",
            "Lints compose/helm/terraform snippets for reproducibility gaps (unpinned images, missing healthchecks).",
            "Environment definitions drift into un-reproducible snowflakes silently.",
            "Platform teams"),
        ("Security", "Dependency choreographer",
            "Grouped, risk-prioritized dependency-update PRs with advisory context for monorepos.",
            "Dependabot floods repos with unbounded update PRs that teams ignore.",
            "Maintainers of actively developed projects"),
    ];

    /// <summary>Deterministic fallback candidates (always available).</summary>
    public static IReadOnlyList<GeneratedCandidate> FallbackCandidates() =>
        FallbackBank.Select((b, i) => new GeneratedCandidate(
            b.Title, b.Summary, b.Problem, b.Target, b.Category)).ToList();

    public static IReadOnlyList<string> ExtractCategoryHint(string text) =>
        PortfolioTaxonomy.All
            .Where(c => c.Keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Name)
            .Distinct()
            .ToList();

    public static async Task<IReadOnlyList<GeneratedCandidate>> GenerateAsync(
        RecommendationInput input,
        ILlmClient llm,
        CancellationToken cancellationToken)
    {
        var fallback = FallbackCandidates();
        if (!llm.IsConfigured)
        {
            return fallback;
        }

        try
        {
            var interests = string.Join(", ", input.Interests);
            var response = await llm.CompleteJsonAsync(
                new Ai.LlmJsonRequest(
                    SystemPrompt: """
                    You brainstorm portfolio project ideas for a developer.
                    Return JSON only:
                    {"ideas":[{"title":"...","summary":"one sentence","problem":"why it is needed","targetUser":"who it serves"}]}
                    with 3 to 5 ideas. No markdown fences. Titles must be plain ASCII.
                    """,
                    UserPrompt:
                    $"Goal: {input.Goal}\nInterests: {interests}\nOptional duration (weeks): {input.DurationWeeks ?? "not set"}\nProduce 3-5 project ideas."),
                cancellationToken);

            var parsed = JsonSerializer.Deserialize<LlmIdeasDto>(response.Json);
            var candidates = parsed?.Ideas
                ?.Where(i => IsUsableText(i.Title) && IsUsableText(i.Summary))
                .Select(i => new GeneratedCandidate(
                    Clean(i.Title!),
                    Clean(i.Summary!),
                    Clean(i.Problem ?? string.Empty),
                    Clean(i.TargetUser ?? string.Empty),
                    GuessCategory($"{i.Title} {i.Summary}")))
                .ToList() ?? [];
            return candidates.Count > 0 ? candidates.Take(MaxCandidates).ToList() : fallback;
        }
        catch (Ai.LlmUnavailableException)
        {
            return fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string GuessCategory(string text)
    {
        var hints = ExtractCategoryHint(text);
        return hints.Count > 0 ? hints[0] : "Developer Tooling";
    }

    private static bool IsUsableText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length is >= 3 and <= 500;

    private static string Clean(string value) => value.Trim().TrimEnd('.', ' ');

    private sealed class LlmIdeasDto
    {
        public List<IdeaDto>? Ideas { get; set; }

        public sealed class IdeaDto
        {
            public string? Title { get; set; }
            public string? Summary { get; set; }
            public string? Problem { get; set; }
            public string? TargetUser { get; set; }
        }
    }
}

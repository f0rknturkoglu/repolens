using System.Text.Json;
using System.Text.RegularExpressions;
using RepoLens.Application.Ai;

namespace RepoLens.Application.IdeaValidation;

/// <summary>One GitHub search query with its provenance.</summary>
public sealed record PlanQuery(string Text, string Source); // source: llm | fallback

public sealed record SearchPlan(IReadOnlyList<PlanQuery> Queries);

/// <summary>
/// Builds the bounded search plan for an idea. The deterministic fallback always
/// runs first and is used when no LLM is configured, when the LLM fails, or when
/// the LLM output fails validation — RepoLens never blocks on an LLM.
/// </summary>
public static class SearchPlanBuilder
{
    public const int MaxQueries = 4;
    public const int MaxQueryLength = 200;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "for", "and", "with", "that", "this", "from", "your", "app", "tool",
        "build", "make", "making", "using", "use", "new", "simple", "easy", "platform", "system",
    };

    /// <summary>Meaningful idea terms (≥3 chars, not stop words) — for similarity and gaps.</summary>
    public static IReadOnlyList<string> IdeaTerms(string idea) =>
        idea
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', ',', ';', ':', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !StopWords.Contains(t) && t.All(char.IsLetterOrDigit))
            .Distinct()
            .ToList();

    /// <summary>Deterministic fallback plan: full idea, noun phrases, dominant terms (bounded).</summary>
    public static IReadOnlyList<PlanQuery> FallbackPlan(string idea)
    {
        var terms = IdeaTerms(idea);
        var phrases = new List<string>();
        for (var i = 0; i < Math.Min(terms.Count, 4) - 1; i++)
        {
            phrases.Add($"{terms[i]} {terms[i + 1]}");
        }

        var queries = new List<PlanQuery>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length is > 2 and <= MaxQueryLength && seen.Add(trimmed))
            {
                queries.Add(new PlanQuery(trimmed, "fallback"));
            }
        }

        Add(idea);
        foreach (var phrase in phrases)
        {
            Add(phrase);
        }

        foreach (var term in terms.Take(3))
        {
            Add(term);
        }

        return queries.Take(MaxQueries).ToList();
    }

    /// <summary>
    /// Tries the LLM for a richer query plan; any failure or invalid output falls
    /// back to the deterministic plan.
    /// </summary>
    public static async Task<SearchPlan> BuildAsync(
        string idea,
        ILlmClient? llm,
        CancellationToken cancellationToken)
    {
        var fallback = FallbackPlan(idea);
        if (llm is null || !llm.IsConfigured)
        {
            return new SearchPlan(fallback);
        }

        try
        {
            var response = await llm.CompleteJsonAsync(
                new Ai.LlmJsonRequest(
                    SystemPrompt: """
                    You produce GitHub repository search queries for idea validation.
                    Return JSON only: {"queries": ["...", ...]} with 2 to 4 queries.
                    Queries must be plain terms (no qualifiers), each 3-200 chars.
                    """,
                    UserPrompt: $"Project idea: {idea}\nProduce 2-4 search queries."),
                cancellationToken);

            var parsed = JsonSerializer.Deserialize<LlmPlanDto>(response.Json);
            var queries = parsed?.Queries
                ?.Where(q => IsUsablePlanQuery(q))
                .Take(MaxQueries)
                .Select(q => new PlanQuery(q.Trim(), "llm"))
                .ToList();
            if (queries is { Count: >= 1 })
            {
                // Merge: LLM queries first (idea intent), fallback as safety net.
                return new SearchPlan([.. queries, .. fallback.Take(2)]);
            }
        }
        catch (Ai.LlmUnavailableException)
        {
            // fall through to deterministic plan
        }
        catch (JsonException)
        {
            // malformed LLM output → deterministic plan
        }

        return new SearchPlan(fallback);
    }

    private sealed class LlmPlanDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("queries")]
        public List<string>? Queries { get; set; }
    }

    public static bool IsUsablePlanQuery(string query) =>
        !string.IsNullOrWhiteSpace(query)
        && query.Length is >= 3 and <= MaxQueryLength
        && !Regex.IsMatch(query, "[:<>\"\\n]"); // no qualifiers/injection shapes
}

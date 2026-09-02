namespace RepoLens.Application.Analysis;

/// <summary>
/// Repository features used by ecosystem analysis (similarity graph, metrics).
/// Evidence-based and deterministic — no LLM and no live API calls.
/// </summary>
public sealed class RepoFeatures
{
    public required long Id { get; init; }
    public required long GitHubId { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public string? PrimaryLanguage { get; init; }
    public required int Stars { get; init; }
    public required int Forks { get; init; }
    public required int OpenIssues { get; init; }
    public required bool IsArchived { get; init; }
    public required bool IsFork { get; init; }
    public DateTimeOffset? PushedAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
}

/// <summary>
/// Deterministic, evidence-based similarity between two repositories. The score
/// is a weighted blend of topic Dice similarity, name-token Dice similarity, and
/// same-primary-language, with empty dimensions excluded and the remainder
/// renormalized. Documented in docs/analysis-limitations.md; chosen over vector
/// similarity so ecosystem analysis works without an embedding provider and is
/// reproducible from stored data alone.
/// </summary>
public static class RepoSimilarity
{
    public const double EdgeThreshold = 0.30;

    private static readonly HashSet<string> StopNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "for", "and", "with", "tool", "library", "lib", "app", "client", "server",
    };

    public static IReadOnlySet<string> NameTokens(string name)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in name.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var part in SplitCamel(raw))
            {
                var token = part.Trim().ToLowerInvariant();
                if (token.Length >= 2 && !StopNameTokens.Contains(token))
                {
                    tokens.Add(token);
                }
            }
        }

        return tokens;
    }

    private static IEnumerable<string> SplitCamel(string value)
    {
        // pgroll -> pgroll; camelBack -> camel, back
        var parts = new List<string>();
        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && (char.IsLower(value[i - 1]) || (i > 1 && char.IsLower(value[i - 2]))))
            {
                parts.Add(value[start..i]);
                start = i;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }

    public static double Score(RepoFeatures a, RepoFeatures b)
    {
        var weights = 0.0;
        var score = 0.0;

        var topicSim = Dice(
            a.Topics.Select(t => t.ToLowerInvariant()).ToHashSet(),
            b.Topics.Select(t => t.ToLowerInvariant()).ToHashSet());
        if (topicSim.HasValue)
        {
            weights += 0.5;
            score += 0.5 * topicSim.Value;
        }

        var nameSim = Dice(NameTokens(a.Name), NameTokens(b.Name));
        if (nameSim.HasValue)
        {
            weights += 0.3;
            score += 0.3 * nameSim.Value;
        }

        if (a.PrimaryLanguage is not null && b.PrimaryLanguage is not null)
        {
            weights += 0.2;
            if (string.Equals(a.PrimaryLanguage, b.PrimaryLanguage, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.2;
            }
        }

        return weights > 0 ? score / weights : 0.0;
    }

    /// <summary>Dice coefficient over two sets; null when either side is empty.</summary>
    public static double? Dice(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return null;
        }

        var overlap = a.Intersect(b).Count();
        return 2.0 * overlap / (a.Count + b.Count);
    }

    /// <summary>Undirected edges (ordered pairs) above the similarity threshold.</summary>
    public static List<(RepoFeatures A, RepoFeatures B, double Score)> Edges(
        IReadOnlyList<RepoFeatures> repos)
    {
        var edges = new List<(RepoFeatures, RepoFeatures, double)>();
        for (var i = 0; i < repos.Count; i++)
        {
            for (var j = i + 1; j < repos.Count; j++)
            {
                var score = Score(repos[i], repos[j]);
                if (score >= EdgeThreshold)
                {
                    edges.Add((repos[i], repos[j], score));
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Connected components of the similarity graph (BFS over the id-ordered
    /// adjacency), returned deterministically ordered by smallest member id and
    /// with members sorted by id.
    /// </summary>
    public static List<List<RepoFeatures>> ConnectedComponents(
        IReadOnlyList<RepoFeatures> repos,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        var ordered = repos.OrderBy(r => r.Id).ToList();
        var adjacency = ordered.ToDictionary(r => r.Id, _ => new List<long>());
        foreach (var (a, b, _) in edges)
        {
            adjacency[a.Id].Add(b.Id);
            adjacency[b.Id].Add(a.Id);
        }

        var seen = new HashSet<long>();
        var components = new List<List<RepoFeatures>>();
        foreach (var seed in ordered)
        {
            if (!seen.Add(seed.Id))
            {
                continue;
            }

            var queue = new Queue<long>();
            queue.Enqueue(seed.Id);
            var members = new List<RepoFeatures>();
            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var current = repos.First(r => r.Id == currentId);
                members.Add(current);
                foreach (var neighbor in adjacency[currentId].Where(n => seen.Add(n)))
                {
                    queue.Enqueue(neighbor);
                }
            }

            components.Add(members.OrderBy(m => m.Id).ToList());
        }

        return components.OrderBy(c => c[0].Id).ToList();
    }
}

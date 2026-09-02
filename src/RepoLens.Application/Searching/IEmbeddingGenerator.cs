namespace RepoLens.Application.Searching;

/// <summary>
/// Generates text embeddings for a provider-agnostic model. The concrete
/// provider and model name come from configuration, never from code. One call
/// may carry several texts (batched); results must align by input order.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Configured model identifier (same value persisted on documents).</summary>
    string Model { get; }

    /// <summary>True when a provider/model is configured at all.</summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}

/// <summary>Raised when embeddings cannot be produced (unconfigured, network, upstream).</summary>
public sealed class EmbeddingUnavailableException(Exception? inner) : Exception(
    "Embedding generation is unavailable.", inner);

/// <summary>Search request filters (minimal set; no filter framework).</summary>
public sealed record SearchFilters(
    string? Language = null,
    bool? Archived = null,
    int? MinStars = null);

/// <summary>One merged result: repository + relative RRF score in [0,1].</summary>
public sealed record RankedRepository(long RepositoryId, double Score, int BestRank);

/// <summary>
/// Reciprocal Rank Fusion: combines two order-only ranked lists (rank 1 = best).
/// Deterministic and documented — score_i = Σ k/(k + rank_in_list); ties break
/// by repository id; duplicate hits across lists are suppressed by construction.
/// Scores are normalized by the maximum so results carry a meaningful 0..1
/// relative relevance instead of raw RRF magnitudes.
/// </summary>
public static class ReciprocalRankFusion
{
    public const int DefaultK = 60;

    public static List<RankedRepository> Merge(
        IReadOnlyList<long> keywordIds,
        IReadOnlyList<long> vectorIds,
        int k = DefaultK)
    {
        var scores = new Dictionary<long, double>();
        var bestRank = new Dictionary<long, int>();

        Accumulate(keywordIds, k, scores, bestRank);
        Accumulate(vectorIds, k, scores, bestRank);

        var maxScore = scores.Count > 0 ? scores.Values.Max() : 1.0;
        return scores
            .Select(kvp => new RankedRepository(
                kvp.Key,
                Math.Clamp(kvp.Value / maxScore, 0, 1),
                bestRank[kvp.Key]))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.RepositoryId)
            .ToList();
    }

    private static void Accumulate(
        IReadOnlyList<long> ids,
        int k,
        Dictionary<long, double> scores,
        Dictionary<long, int> bestRank)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var repositoryId = ids[i];
            var rank = i + 1;
            scores[repositoryId] = scores.GetValueOrDefault(repositoryId) + k / (double)(k + rank);
            if (!bestRank.ContainsKey(repositoryId) || rank < bestRank[repositoryId])
            {
                bestRank[repositoryId] = rank;
            }
        }
    }
}

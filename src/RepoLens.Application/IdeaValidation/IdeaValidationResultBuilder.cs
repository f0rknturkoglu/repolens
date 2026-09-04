using System.Text.Json;
using RepoLens.Application.Analysis;

namespace RepoLens.Application.IdeaValidation;

/// <summary>API response for an idea validation (typed, evidence-backed).</summary>
public sealed class IdeaValidationResponse
{
    public required long Id { get; init; }
    public required string IdeaText { get; init; }
    public required string Version { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required bool ServedFromCache { get; init; }
    public NoveltyDto? Novelty { get; init; }
    public IReadOnlyList<PlanQueryDto>? QueryPlan { get; init; }
    public IReadOnlyList<ClusterSummaryDto>? Clusters { get; init; }
    public IReadOnlyList<string>? Evidence { get; init; }
    public IReadOnlyList<GapHypothesisDto>? Gaps { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = [];

    public sealed class NoveltyDto
    {
        public required double Score { get; init; }
        public required string FormulaVersion { get; init; }
        public required IReadOnlyList<ComponentDto> Components { get; init; }
        public required IReadOnlyList<CompetitorDto> Competitors { get; init; }
    }

    public sealed class ComponentDto
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required double Value { get; init; }
        public required string Evidence { get; init; }
        public int WeightPercent { get; init; }
    }

    public sealed class CompetitorDto
    {
        public long? RepositoryId { get; init; }
        public required string FullName { get; init; }
        public string? PrimaryLanguage { get; init; }
        public required int Stars { get; init; }
        public required bool IsArchived { get; init; }
        public required double Overlap { get; init; }
        public required IReadOnlyList<string> Reasons { get; init; }
    }

    public sealed record PlanQueryDto(string Text, string Source);

    public sealed record ClusterSummaryDto(string Label, int MemberCount, IReadOnlyList<string> TopRepositories);

    public sealed record GapHypothesisDto(string Kind, string Statement);
}

/// <summary>Assembles stored idea validations into API responses.</summary>
public sealed class IdeaValidationResultBuilder(
    IIdeaValidationStore ideaStore,
    IAnalysisStore analysisStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static readonly IReadOnlyList<string> Limitations =
    [
        "Candidates come from a bounded set of GitHub searches run at analysis time — the report describes that set, not all of GitHub.",
        "Similarity uses topics, names, and language only; repositories without enrichment (README/topics) contribute less signal.",
        "Estimated Novelty is a heuristic derived from the analyzed set, not a scientific or market claim.",
        "Gap statements are hypotheses from the analyzed candidate set and may miss approaches that different search terms would find.",
    ];

    public async Task<IdeaValidationResponse?> BuildAsync(long id, bool servedFromCache, CancellationToken cancellationToken)
    {
        var validation = await ideaStore.GetAsync(id, cancellationToken);
        if (validation is null)
        {
            return null;
        }

        IdeaValidationResponse.NoveltyDto? novelty = null;
        IReadOnlyList<IdeaValidationResponse.PlanQueryDto>? plan = null;
        IReadOnlyList<IdeaValidationResponse.ClusterSummaryDto>? clusters = null;
        var evidence = new List<string>();
        IReadOnlyList<IdeaValidationResponse.GapHypothesisDto>? gaps = null;

        if (validation.NoveltyJson is not null)
        {
            var storedNovelty = JsonSerializer.Deserialize<NoveltyResult>(validation.NoveltyJson, JsonOptions);
            if (storedNovelty is not null)
            {
                novelty = new IdeaValidationResponse.NoveltyDto
                {
                    Score = storedNovelty.Score,
                    FormulaVersion = storedNovelty.FormulaVersion,
                    Components = storedNovelty.Components
                        .Select(c => new IdeaValidationResponse.ComponentDto
                        {
                            Key = c.Key,
                            Label = c.Label,
                            Value = c.Value,
                            Evidence = c.Evidence,
                            WeightPercent = c.WeightPercent > 0 ? c.WeightPercent : DefaultNoveltyWeight(c.Key),
                        })
                        .ToList(),
                    Competitors = storedNovelty.Competitors
                        .Select(c => new IdeaValidationResponse.CompetitorDto
                        {
                            RepositoryId = c.RepositoryId > 0 ? c.RepositoryId : null,
                            FullName = c.FullName,
                            PrimaryLanguage = c.PrimaryLanguage,
                            Stars = c.Stars,
                            IsArchived = c.IsArchived,
                            Overlap = c.Overlap,
                            Reasons = c.Reasons,
                        })
                        .ToList(),
                };
                evidence.AddRange(storedNovelty.Components.Select(c => c.Evidence));
            }
        }

        if (validation.SearchPlanJson is not null)
        {
            plan = JsonSerializer.Deserialize<List<IdeaValidationResponse.PlanQueryDto>>(validation.SearchPlanJson, JsonOptions);
        }

        if (validation.MetricsJson is not null && validation.MetricsJson != "null")
        {
            var metrics = EcosystemMetricsCalculator.Deserialize(validation.MetricsJson);
            evidence.InsertRange(0, metrics.Evidence);
        }

        if (validation.ClustersJson is not null && validation.ClustersJson != "[]")
        {
            clusters = await BuildClustersAsync(validation.ClustersJson, cancellationToken);
        }

        if (validation.GapsJson is not null)
        {
            gaps = JsonSerializer.Deserialize<List<IdeaValidationResponse.GapHypothesisDto>>(validation.GapsJson, JsonOptions);
        }

        return new IdeaValidationResponse
        {
            Id = validation.Id,
            IdeaText = validation.IdeaText,
            Version = validation.Version,
            Status = validation.Status.ToString().ToLowerInvariant(),
            CreatedAtUtc = validation.CreatedAtUtc,
            CompletedAtUtc = validation.CompletedAtUtc,
            ServedFromCache = servedFromCache,
            Novelty = novelty,
            QueryPlan = plan,
            Clusters = clusters,
            Evidence = evidence,
            Gaps = gaps,
            Limitations = Limitations,
        };
    }

    private async Task<IReadOnlyList<IdeaValidationResponse.ClusterSummaryDto>> BuildClustersAsync(
        string clustersJson,
        CancellationToken cancellationToken)
    {
        var stored = JsonSerializer.Deserialize<List<StoredCluster>>(clustersJson, JsonOptions) ?? [];
        if (stored.Count == 0)
        {
            return [];
        }

        var memberIds = stored.SelectMany(c => c.Members!).Select(m => m.RepositoryId).Distinct().ToList();
        var features = await analysisStore.GetRepoFeaturesAsync(memberIds, cancellationToken);
        var byId = features.ToDictionary(f => f.Id);

        return stored.Select(c => new IdeaValidationResponse.ClusterSummaryDto(
            c.Label ?? "Cluster",
            c.Members?.Count ?? 0,
            (c.Members ?? [])
                .Where(m => byId.ContainsKey(m.RepositoryId))
                .Take(6)
                .Select(m => byId[m.RepositoryId].FullName)
                .ToList())).ToList();
    }

    private static int DefaultNoveltyWeight(string key) => key switch
    {
        "competitor_gap" => IdeaNoveltyScorer.WeightClosestCompetitorPercent,
        "density" => IdeaNoveltyScorer.WeightDensityPercent,
        "cluster_dominance" => IdeaNoveltyScorer.WeightClusterDominancePercent,
        "active_competition" => IdeaNoveltyScorer.WeightActiveCompetitionPercent,
        "concept_redundancy" => IdeaNoveltyScorer.WeightConceptRedundancyPercent,
        "candidates" => 100,
        _ => 20,
    };

    private sealed class StoredCluster
    {
        public string? Label { get; init; }
        public List<StoredMember>? Members { get; init; }
    }

    private sealed class StoredMember
    {
        public long RepositoryId { get; init; }
        public double Centrality { get; init; }
    }
}

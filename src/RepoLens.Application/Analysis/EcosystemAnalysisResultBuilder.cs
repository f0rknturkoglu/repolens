using System.Text.Json;
using RepoLens.Domain.Analysis;

namespace RepoLens.Application.Analysis;

/// <summary>API response for an ecosystem analysis (typed + evidence-backed).</summary>
public sealed class EcosystemAnalysisResponse
{
    public required long Id { get; init; }
    public required string Query { get; init; }
    public required string Version { get; init; }
    public required string Status { get; init; }
    public string? Error { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public EcosystemMetrics? Metrics { get; init; }
    public IReadOnlyList<string>? Evidence { get; init; }
    public IReadOnlyList<ClusterDto> Clusters { get; init; } = [];
    public IReadOnlyList<VariantDto>? QueryVariants { get; init; }

    public sealed class ClusterDto
    {
        public required string Label { get; init; }
        public required int MemberCount { get; init; }
        public required IReadOnlyList<MemberDto> Members { get; init; }
    }

    public sealed class MemberDto
    {
        public required long RepositoryId { get; init; }
        public required double Centrality { get; init; }
        public required MemberRepositoryDto Repository { get; init; }
    }

    public sealed class MemberRepositoryDto
    {
        public required string FullName { get; init; }
        public string? Description { get; init; }
        public string? PrimaryLanguage { get; init; }
        public required int Stars { get; init; }
        public required bool IsArchived { get; init; }
        public required string HtmlUrl { get; init; }
    }

    public sealed record VariantDto(string Label, string Query);
}

/// <summary>Assembles stored analysis snapshots into API responses.</summary>
public sealed class EcosystemAnalysisResultBuilder(IAnalysisStore store)
{
    public async Task<EcosystemAnalysisResponse?> BuildAsync(long id, CancellationToken cancellationToken)
    {
        var analysis = await store.GetAsync(id, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        return await BuildFromAsync(analysis, cancellationToken);
    }

    private async Task<EcosystemAnalysisResponse> BuildFromAsync(
        EcosystemAnalysis analysis,
        CancellationToken cancellationToken)
    {
        EcosystemMetrics? metrics = null;
        IReadOnlyList<string>? evidence = null;
        IReadOnlyList<EcosystemAnalysisResponse.VariantDto>? variants = null;

        if (analysis.Status == EcosystemAnalysisStatus.Completed && analysis.MetricsJson is not null)
        {
            metrics = EcosystemMetricsCalculator.Deserialize(analysis.MetricsJson);
            evidence = metrics.Evidence;
        }

        if (analysis.VariantsJson is not null)
        {
            variants = JsonSerializer.Deserialize<List<EcosystemAnalysisResponse.VariantDto>>(
                analysis.VariantsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var clusters = await BuildClustersAsync(analysis.Id, cancellationToken);

        return new EcosystemAnalysisResponse
        {
            Id = analysis.Id,
            Query = analysis.Query,
            Version = analysis.Version,
            Status = analysis.Status.ToString().ToLowerInvariant(),
            Error = analysis.Error == EcosystemAnalysisError.None ? null : analysis.Error.ToString(),
            CreatedAtUtc = analysis.CreatedAtUtc,
            CompletedAtUtc = analysis.CompletedAtUtc,
            Metrics = metrics,
            Evidence = evidence,
            Clusters = clusters,
            QueryVariants = variants,
        };
    }

    private async Task<IReadOnlyList<EcosystemAnalysisResponse.ClusterDto>> BuildClustersAsync(
        long analysisId,
        CancellationToken cancellationToken)
    {
        var clusters = await store.GetClustersAsync(analysisId, cancellationToken);
        if (clusters.Count == 0)
        {
            return [];
        }

        var memberIds = clusters
            .SelectMany(c => DeserializeMembers(c.MembersJson))
            .Select(m => m.RepositoryId)
            .Distinct()
            .ToList();
        var features = await store.GetRepoFeaturesAsync(memberIds, cancellationToken);
        var byId = features.ToDictionary(f => f.Id);

        return clusters.Select(cluster =>
        {
            var members = DeserializeMembers(cluster.MembersJson);
            return new EcosystemAnalysisResponse.ClusterDto
            {
                Label = cluster.Label,
                MemberCount = members.Count,
                Members = members
                    .Where(m => byId.ContainsKey(m.RepositoryId))
                    .Select(member => new EcosystemAnalysisResponse.MemberDto
                    {
                        RepositoryId = member.RepositoryId,
                        Centrality = member.Centrality,
                        Repository = new EcosystemAnalysisResponse.MemberRepositoryDto
                        {
                            FullName = byId[member.RepositoryId].FullName,
                            Description = byId[member.RepositoryId].Description,
                            PrimaryLanguage = byId[member.RepositoryId].PrimaryLanguage,
                            Stars = byId[member.RepositoryId].Stars,
                            IsArchived = byId[member.RepositoryId].IsArchived,
                            HtmlUrl = byId[member.RepositoryId].FullName.Length > 0
                                ? $"https://github.com/{byId[member.RepositoryId].FullName}"
                                : string.Empty,
                        },
                    })
                    .ToList(),
            };
        }).ToList();
    }

    private static List<ClusterMember> DeserializeMembers(string json) =>
        JsonSerializer.Deserialize<List<ClusterMember>>(json)
        ?? throw new InvalidOperationException("Stored cluster members are unreadable.");
}

using System.Text.Json;
using RepoLens.Application.Analysis;

namespace RepoLens.Application.Analysis;

/// <summary>A computed cluster: label + centrality-ordered members (ids only).</summary>
public sealed record ComputedCluster(string Label, IReadOnlyList<ClusterMember> Members);

/// <summary>
/// Pure cluster computation shared by ecosystem analysis and idea validation:
/// similarity graph edges → connected components → centrality ordering and
/// heuristic labels. Deterministic and unit-tested (ADR 002).
/// </summary>
public static class Clusterizer
{
    public static List<ComputedCluster> Build(
        IReadOnlyList<RepoFeatures> features,
        List<(RepoFeatures A, RepoFeatures B, double Score)> edges)
    {
        var components = RepoSimilarity.ConnectedComponents(features, edges);
        var labelIndex = 0;
        var clusters = new List<ComputedCluster>();

        foreach (var component in components)
        {
            var label = LabelFor(component, ref labelIndex);
            var centrality = component.ToDictionary(
                member => member.Id,
                member => component.Count > 1
                    ? component.Where(other => other.Id != member.Id)
                        .Average(other => RepoSimilarity.Score(member, other))
                    : 0.0);

            var members = component
                .Select(member => new ClusterMember(member.Id, Math.Round(centrality[member.Id], 4)))
                .OrderByDescending(m => m.Centrality)
                .ThenBy(m => m.RepositoryId)
                .ToList();

            clusters.Add(new ComputedCluster(label, members));
        }

        return clusters;
    }

    private static string LabelFor(List<RepoFeatures> component, ref int labelIndex)
    {
        if (component.Count >= 2)
        {
            var languageShare = component
                .Where(m => m.PrimaryLanguage is not null)
                .GroupBy(m => m.PrimaryLanguage!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => (Language: g.Key, Count: g.Count()))
                .FirstOrDefault();
            if (languageShare.Language is not null && languageShare.Count / (double)component.Count >= 0.5)
            {
                return $"{languageShare.Language} projects";
            }

            var topicShare = component
                .SelectMany(m => m.Topics)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => (Topic: g.Key, Count: g.Count()))
                .FirstOrDefault();
            if (topicShare.Topic is not null && topicShare.Count / (double)component.Count >= 0.4)
            {
                return $"{topicShare.Topic} ecosystem";
            }
        }

        labelIndex++;
        return component.Count == 1 ? "Standalone" : $"Cluster {labelIndex}";
    }

    public static string SerializeMembers(IReadOnlyList<ClusterMember> members) =>
        JsonSerializer.Serialize(members);

    public static List<ClusterMember> DeserializeMembers(string json) =>
        JsonSerializer.Deserialize<List<ClusterMember>>(json)
        ?? throw new InvalidOperationException("Stored cluster members are unreadable.");
}

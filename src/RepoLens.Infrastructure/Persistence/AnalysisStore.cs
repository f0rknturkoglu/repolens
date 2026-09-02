using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Analysis;
using RepoLens.Domain.Analysis;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>EF implementation of <see cref="IAnalysisStore"/>.</summary>
public sealed class AnalysisStore(RepoLensDbContext db) : IAnalysisStore
{
    public async Task<EcosystemAnalysis> BeginAsync(
        string query,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var analysis = EcosystemAnalysis.Start(query, createdAtUtc);
        db.EcosystemAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
        return analysis;
    }

    public async Task CompleteAsync(
        long analysisId,
        string metricsJson,
        string variantsJson,
        IReadOnlyList<CandidateHit> candidates,
        IReadOnlyList<EcosystemCluster> clusters,
        CancellationToken cancellationToken)
    {
        var analysis = await db.EcosystemAnalyses.SingleAsync(a => a.Id == analysisId, cancellationToken);
        analysis.Complete(metricsJson, variantsJson, DateTimeOffset.UtcNow);

        foreach (var hit in candidates)
        {
            db.EcosystemAnalysisCandidates.Add(new EcosystemAnalysisCandidate(
                analysisId, hit.RepositoryId, hit.Variant, hit.Rank));
        }

        db.EcosystemClusters.AddRange(clusters);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(long analysisId, EcosystemAnalysisError error, CancellationToken cancellationToken)
    {
        var analysis = await db.EcosystemAnalyses.SingleAsync(a => a.Id == analysisId, cancellationToken);
        analysis.Fail(error, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EcosystemAnalysis?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.EcosystemAnalyses.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EcosystemCluster>> GetClustersAsync(
        long analysisId,
        CancellationToken cancellationToken) =>
        await db.EcosystemClusters
            .Where(c => c.AnalysisId == analysisId)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<long>> GetCandidateRepositoryIdsAsync(
        long analysisId,
        CancellationToken cancellationToken) =>
        await db.EcosystemAnalysisCandidates
            .Where(c => c.AnalysisId == analysisId)
            .OrderBy(c => c.RepositoryId)
            .Select(c => c.RepositoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RepoFeatures>> GetRepoFeaturesAsync(
        IReadOnlyList<long> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return [];
        }

        var repos = await db.Repositories
            .Where(r => repositoryIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var topics = await db.RepositoryTopics
            .Where(t => repositoryIds.Contains(t.RepositoryId))
            .GroupBy(t => t.RepositoryId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(t => t.Topic).OrderBy(t => t).ToList(), cancellationToken);

        return repos.Select(r => new RepoFeatures
        {
            Id = r.Id,
            GitHubId = r.GitHubId,
            Name = r.Name,
            FullName = r.FullName,
            Description = r.Description,
            PrimaryLanguage = r.PrimaryLanguage,
            Stars = r.Stars,
            Forks = r.Forks,
            OpenIssues = r.OpenIssues,
            IsArchived = r.IsArchived,
            IsFork = r.IsFork,
            PushedAtUtc = r.PushedAt,
            CreatedAtUtc = r.CreatedAt,
            Topics = topics.GetValueOrDefault(r.Id) ?? [],
        }).ToList();
    }
}

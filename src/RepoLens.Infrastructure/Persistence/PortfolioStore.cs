using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Portfolio;
using RepoLens.Domain.Portfolio;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>EF implementation of <see cref="IPortfolioStore"/>.</summary>
public sealed class PortfolioStore(RepoLensDbContext db) : IPortfolioStore
{
    public async Task<PortfolioAnalysis> BeginAsync(
        string username,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var analysis = PortfolioAnalysis.Start(username, createdAtUtc);
        db.PortfolioAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
        return analysis;
    }

    public async Task CompleteAsync(
        long id,
        int analyzedRepositoryCount,
        int totalRepositoryCount,
        string signalsJson,
        string coverageJson,
        CancellationToken cancellationToken)
    {
        var analysis = await db.PortfolioAnalyses.SingleAsync(a => a.Id == id, cancellationToken);
        analysis.Complete(
            analyzedRepositoryCount, totalRepositoryCount, signalsJson, coverageJson,
            DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(long id, CancellationToken cancellationToken)
    {
        var analysis = await db.PortfolioAnalyses.SingleAsync(a => a.Id == id, cancellationToken);
        analysis.Fail(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PortfolioAnalysis?> GetRecentAsync(string username, CancellationToken cancellationToken) =>
        db.PortfolioAnalyses
            .Where(a => a.Username == username)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PortfolioAnalysis?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.PortfolioAnalyses.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<IReadOnlyDictionary<long, long>> GetInternalIdsAsync(
        IReadOnlyCollection<long> gitHubIds,
        CancellationToken cancellationToken) =>
        db.Repositories
            .Where(r => gitHubIds.Contains(r.GitHubId))
            .Select(r => new { r.GitHubId, r.Id })
            .ToDictionaryAsync(r => r.GitHubId, r => r.Id, cancellationToken)
            .ContinueWith(
                t => (IReadOnlyDictionary<long, long>)t.Result,
                cancellationToken);
}

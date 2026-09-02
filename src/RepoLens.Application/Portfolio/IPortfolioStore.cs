using RepoLens.Domain.Portfolio;

namespace RepoLens.Application.Portfolio;

/// <summary>Persistence port for portfolio analyses.</summary>
public interface IPortfolioStore
{
    Task<PortfolioAnalysis> BeginAsync(string username, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);

    Task CompleteAsync(
        long id,
        int analyzedRepositoryCount,
        int totalRepositoryCount,
        string signalsJson,
        string coverageJson,
        CancellationToken cancellationToken);

    Task FailAsync(long id, CancellationToken cancellationToken);

    /// <summary>Most recent completed analysis for a username, if any.</summary>
    Task<PortfolioAnalysis?> GetRecentAsync(string username, CancellationToken cancellationToken);

    Task<PortfolioAnalysis?> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>Map GitHub ids to internal repository ids for rows that already exist.</summary>
    Task<IReadOnlyDictionary<long, long>> GetInternalIdsAsync(
        IReadOnlyCollection<long> gitHubIds,
        CancellationToken cancellationToken);
}

using RepoLens.Domain.Discovery;

namespace RepoLens.Application.Discovery;

/// <summary>
/// Persistence port for discovered repositories. Implemented in Infrastructure
/// over EF Core; the Application layer stays persistence-agnostic.
/// </summary>
public interface IRepositoryStore
{
    /// <summary>
    /// Inserts repositories whose GitHub id is new and applies discovered
    /// metadata to existing rows (identity = GitHub id, never duplicated).
    /// Returns the canonical persisted entities in input order so callers can
    /// safely use database ids for both newly inserted and existing rows.
    /// </summary>
    Task<IReadOnlyList<Repository>> UpsertAsync(
        IReadOnlyList<Repository> repositories,
        CancellationToken cancellationToken);
}

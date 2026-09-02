using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Discovery;
using RepoLens.Domain.Discovery;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRepositoryStore"/>. Upsert is
/// load-then-merge: rows whose GitHub id already exists get refreshed metadata,
/// unknown ids are inserted — identity is the GitHub id, never duplicated.
/// </summary>
public sealed class RepositoryStore(RepoLensDbContext db) : IRepositoryStore
{
    public async Task UpsertAsync(
        IReadOnlyList<Repository> repositories,
        CancellationToken cancellationToken)
    {
        if (repositories.Count == 0)
        {
            return;
        }

        var gitHubIds = repositories.Select(r => r.GitHubId).ToHashSet();
        var existing = await db.Repositories
            .Where(r => gitHubIds.Contains(r.GitHubId))
            .ToDictionaryAsync(r => r.GitHubId, cancellationToken);

        foreach (var repository in repositories)
        {
            if (existing.TryGetValue(repository.GitHubId, out var current))
            {
                current.ApplyDiscoveredMetadata(ToData(repository));
            }
            else
            {
                db.Repositories.Add(repository);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Repository.RepositoryData ToData(Repository repository) => new(
        repository.GitHubId,
        repository.Owner,
        repository.Name,
        repository.FullName,
        repository.Description,
        repository.HtmlUrl,
        repository.DefaultBranch,
        repository.PrimaryLanguage,
        repository.Stars,
        repository.Forks,
        repository.OpenIssues,
        repository.IsArchived,
        repository.IsFork,
        repository.LicenseSpdx,
        repository.CreatedAt,
        repository.UpdatedAt,
        repository.PushedAt);
}

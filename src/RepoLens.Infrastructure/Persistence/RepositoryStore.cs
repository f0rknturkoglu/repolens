using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Discovery;
using RepoLens.Application.Enrichment;
using RepoLens.Domain.Discovery;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRepositoryStore"/> (upsert) and
/// <see cref="IRepositoryDetailReader"/> (enriched aggregate read). Upsert is
/// load-then-merge: rows whose GitHub id already exists get refreshed metadata,
/// unknown ids are inserted — identity is the GitHub id, never duplicated.
/// </summary>
public sealed class RepositoryStore(RepoLensDbContext db) : IRepositoryStore, IRepositoryDetailReader
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

    public async Task<RepositoryDetailSource?> GetAsync(
        long repositoryId,
        CancellationToken cancellationToken)
    {
        var repository = await db.Repositories
            .SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            return null;
        }

        var topics = await db.RepositoryTopics
            .Where(t => t.RepositoryId == repositoryId)
            .OrderBy(t => t.Topic)
            .Select(t => t.Topic)
            .ToListAsync(cancellationToken);

        var languages = await db.RepositoryLanguages
            .Where(l => l.RepositoryId == repositoryId)
            .Select(l => new RepositoryDetailSource.LanguageShare(l.Language, l.Bytes))
            .ToListAsync(cancellationToken);

        var readme = await db.RepositoryReadmes
            .SingleOrDefaultAsync(r => r.RepositoryId == repositoryId, cancellationToken);

        return new RepositoryDetailSource
        {
            Id = repository.Id,
            GitHubId = repository.GitHubId,
            Owner = repository.Owner,
            Name = repository.Name,
            FullName = repository.FullName,
            Description = repository.Description,
            HtmlUrl = repository.HtmlUrl,
            DefaultBranch = repository.DefaultBranch,
            PrimaryLanguage = repository.PrimaryLanguage,
            Stars = repository.Stars,
            Forks = repository.Forks,
            OpenIssues = repository.OpenIssues,
            IsArchived = repository.IsArchived,
            IsFork = repository.IsFork,
            LicenseSpdx = repository.LicenseSpdx,
            CreatedAtUtc = repository.CreatedAt,
            UpdatedAtUtc = repository.UpdatedAt,
            PushedAtUtc = repository.PushedAt,
            DiscoveredAtUtc = repository.DiscoveredAtUtc,
            EnrichedAtUtc = repository.EnrichedAtUtc,
            Topics = topics,
            Languages = languages,
            ReadmeText = readme?.TextContent,
            ReadmeContentHash = readme?.ContentHash,
            ReadmeFetchedAtUtc = readme?.FetchedAtUtc,
        };
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

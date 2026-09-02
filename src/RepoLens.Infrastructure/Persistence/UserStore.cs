using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Identity;
using RepoLens.Domain.Identity;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>EF implementation of <see cref="IUserStore"/>.</summary>
public sealed class UserStore(RepoLensDbContext db) : IUserStore
{
    public async Task<User> UpsertAsync(User user, CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .SingleOrDefaultAsync(u => u.GitHubId == user.GitHubId, cancellationToken);
        if (existing is null)
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }

        existing.RecordLogin(user.Name, user.AvatarUrl, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<User?> GetByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.GitHubId == gitHubId, cancellationToken);

    public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task AddSavedAnalysisAsync(SavedAnalysis saved, CancellationToken cancellationToken)
    {
        db.SavedAnalyses.Add(saved);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedAnalysis>> ListSavedAnalysesAsync(
        long userId,
        int limit,
        CancellationToken cancellationToken) =>
        await db.SavedAnalyses
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}

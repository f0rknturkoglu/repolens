using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Recommendation;
using RepoLens.Domain.Recommendation;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>EF implementation of <see cref="IRecommendationStore"/>.</summary>
public sealed class RecommendationStore(RepoLensDbContext db) : IRecommendationStore
{
    public async Task<RecommendationRequest> BeginAsync(
        string goal,
        string requestHash,
        string? username,
        string interestsJson,
        string constraintsJson,
        CancellationToken cancellationToken)
    {
        var request = RecommendationRequest.Start(
            goal, requestHash, username, interestsJson, constraintsJson, DateTimeOffset.UtcNow);
        db.RecommendationRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task CompleteAsync(long id, string resultJson, CancellationToken cancellationToken)
    {
        var request = await db.RecommendationRequests.SingleAsync(r => r.Id == id, cancellationToken);
        request.Complete(resultJson, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(long id, CancellationToken cancellationToken)
    {
        var request = await db.RecommendationRequests.SingleAsync(r => r.Id == id, cancellationToken);
        request.Fail(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<RecommendationRequest?> GetRecentCompletedAsync(
        string requestHash,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken) =>
        db.RecommendationRequests
            .Where(r => r.RequestHash == requestHash
                        && r.Status == RecommendationStatus.Completed
                        && r.CreatedAtUtc >= createdAfter)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RecommendationRequest?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.RecommendationRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
}

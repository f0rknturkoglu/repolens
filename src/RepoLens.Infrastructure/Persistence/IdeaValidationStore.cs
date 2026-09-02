using Microsoft.EntityFrameworkCore;
using RepoLens.Application.IdeaValidation;
using RepoLens.Domain.Analysis;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>EF implementation of <see cref="IIdeaValidationStore"/>.</summary>
public sealed class IdeaValidationStore(RepoLensDbContext db) : IIdeaValidationStore
{
    public async Task<IdeaValidation> BeginAsync(
        string ideaText,
        string ideaHash,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var validation = IdeaValidation.Start(ideaText, ideaHash, createdAtUtc);
        db.IdeaValidations.Add(validation);
        await db.SaveChangesAsync(cancellationToken);
        return validation;
    }

    public async Task CompleteAsync(
        long id,
        string searchPlanJson,
        string metricsJson,
        string clustersJson,
        string competitorsJson,
        string noveltyJson,
        string gapsJson,
        CancellationToken cancellationToken)
    {
        var validation = await db.IdeaValidations.SingleAsync(v => v.Id == id, cancellationToken);
        validation.Complete(
            searchPlanJson, metricsJson, clustersJson, competitorsJson, noveltyJson, gapsJson,
            DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(long id, CancellationToken cancellationToken)
    {
        var validation = await db.IdeaValidations.SingleAsync(v => v.Id == id, cancellationToken);
        validation.Fail(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<IdeaValidation?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.IdeaValidations.SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<IdeaValidation?> GetRecentCompletedAsync(
        string ideaHash,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken) =>
        db.IdeaValidations
            .Where(v => v.IdeaHash == ideaHash
                        && v.Status == IdeaValidationStatus.Completed
                        && v.CreatedAtUtc >= createdAfter)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}

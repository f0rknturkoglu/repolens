
namespace RepoLens.Application.IdeaValidation;

using IdeaValidationEntity = RepoLens.Domain.Analysis.IdeaValidation;

/// <summary>Persistence port for idea-validation sessions.</summary>
public interface IIdeaValidationStore
{
    Task<IdeaValidationEntity> BeginAsync(
        string ideaText,
        string ideaHash,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        long id,
        string searchPlanJson,
        string metricsJson,
        string clustersJson,
        string competitorsJson,
        string noveltyJson,
        string gapsJson,
        CancellationToken cancellationToken);

    Task FailAsync(long id, CancellationToken cancellationToken);

    Task<IdeaValidationEntity?> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>Most recent completed validation for an idea hash created within the window.</summary>
    Task<IdeaValidationEntity?> GetRecentCompletedAsync(
        string ideaHash,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken);
}

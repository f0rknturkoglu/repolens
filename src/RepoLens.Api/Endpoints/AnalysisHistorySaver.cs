using RepoLens.Api.Identity;
using RepoLens.Application.Identity;
using RepoLens.Domain.Identity;

namespace RepoLens.Api.Endpoints;

/// <summary>
/// Records analysis runs in the signed-in user's history when a session exists.
/// Silent when anonymous — history is a signed-in convenience, never a gate.
/// </summary>
public static class AnalysisHistorySaver
{
    public static async Task SaveAsync(
        AuthSessionService sessions,
        IUserStore users,
        string kind,
        long referenceId,
        string title,
        string status,
        string version,
        CancellationToken cancellationToken)
    {
        var current = await sessions.GetCurrentUserAsync(cancellationToken);
        if (current is null)
        {
            return;
        }

        await users.AddSavedAnalysisAsync(
            new SavedAnalysis(
                current.UserId,
                kind,
                referenceId,
                title.Length > 300 ? title[..300] : title,
                status,
                version,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }
}

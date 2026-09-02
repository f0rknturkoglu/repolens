using RepoLens.Application.Discovery;
using RepoLens.Domain.Discovery;
using RepoLens.Domain.Enrichment;

namespace RepoLens.Application.Enrichment;

/// <summary>
/// Executes one repository enrichment job: fetch detail/README/topics/languages
/// from GitHub, decide the snapshot, and persist everything atomically. Retry
/// policy is bounded and encoded here: rate limits wait for the reset, transient
/// errors back off exponentially, permanent errors (repository gone) fail fast.
/// </summary>
public sealed class RepositoryEnrichmentProcessor(
    IGitHubRepositoryClient gitHub,
    ITextNormalizer textNormalizer,
    IEnrichmentJobStore jobs,
    EnrichmentSettings settings)
{
    public async Task ProcessAsync(EnrichmentJob job, CancellationToken cancellationToken)
    {
        var repository = await jobs.GetRepositoryAsync(job.RepositoryId, cancellationToken);
        if (repository is null)
        {
            await jobs.FailAsync(
                job.Id, EnrichmentErrorCategory.Permanent,
                "Repository row no longer exists.", cancellationToken);
            return;
        }

        try
        {
            var result = await BuildResultAsync(repository, cancellationToken);
            await jobs.CompleteAsync(job.Id, result, cancellationToken);
        }
        catch (GitHubRateLimitExceededException ex)
        {
            await HandleRateLimitAsync(job, ex, cancellationToken);
        }
        catch (GitHubRequestRejectedException ex) when (ex.StatusCode is 404 or 410 or 451)
        {
            await jobs.FailAsync(
                job.Id, EnrichmentErrorCategory.Permanent,
                "Repository is no longer accessible on GitHub.", cancellationToken);
        }
        catch (Exception ex) when (ex is GitHubUnavailableException
                                   or GitHubUpstreamErrorException
                                   or GitHubRequestRejectedException)
        {
            await HandleTransientAsync(job, ex, cancellationToken);
        }
    }

    private async Task<RepositoryEnrichmentResult> BuildResultAsync(
        Repository repository,
        CancellationToken cancellationToken)
    {
        var detail = await gitHub.GetRepositoryDetailAsync(repository.GitHubId, cancellationToken);

        // These three fetches are independent; run them concurrently.
        var topicsTask = gitHub.GetTopicsAsync(repository.GitHubId, cancellationToken);
        var languagesTask = gitHub.GetLanguagesAsync(repository.GitHubId, cancellationToken);
        var readmeTask = gitHub.GetReadmeAsync(repository.GitHubId, cancellationToken);
        await Task.WhenAll(topicsTask, languagesTask, readmeTask);

        var readme = NormalizeReadme(await readmeTask);
        var snapshot = await DecideSnapshotAsync(repository, detail, cancellationToken);

        return new RepositoryEnrichmentResult
        {
            Repository = new Repository.RepositoryData(
                detail.GitHubId,
                detail.Owner,
                detail.Name,
                detail.FullName,
                detail.Description,
                detail.HtmlUrl,
                detail.DefaultBranch,
                detail.PrimaryLanguage,
                detail.Stars,
                detail.Forks,
                detail.OpenIssues,
                detail.IsArchived,
                detail.IsFork,
                detail.LicenseSpdx,
                detail.CreatedAt,
                detail.UpdatedAt,
                detail.PushedAt),
            Topics = await topicsTask,
            Languages = await languagesTask,
            Readme = readme,
            Snapshot = snapshot,
        };
    }

    private GitHubReadmeContent? NormalizeReadme(GitHubReadmeContent? readme)
    {
        if (readme is null)
        {
            return null;
        }

        var raw = readme.RawContent;
        if (raw.Length > settings.ReadmeMaxLength)
        {
            raw = raw[..settings.ReadmeMaxLength];
        }

        return new GitHubReadmeContent
        {
            RawContent = raw,
            TextContent = textNormalizer.ToPlainText(raw),
            ContentHash = readme.ContentHash,
        };
    }

    private async Task<RepositorySnapshot?> DecideSnapshotAsync(
        Repository repository,
        GitHubRepositoryDetail detail,
        CancellationToken cancellationToken)
    {
        var latest = await jobs.GetLatestSnapshotAsync(repository.Id, cancellationToken);

        var metricsChanged = latest is null
            || latest.Value.Stars != detail.Stars
            || latest.Value.Forks != detail.Forks
            || latest.Value.OpenIssues != detail.OpenIssues;
        var intervalElapsed = latest is null
            || DateTimeOffset.UtcNow - latest.Value.CapturedAtUtc >= settings.SnapshotMinInterval;

        return metricsChanged && intervalElapsed
            ? new RepositorySnapshot(
                repository.Id, detail.Stars, detail.Forks, detail.OpenIssues, DateTimeOffset.UtcNow)
            : null;
    }

    private async Task HandleRateLimitAsync(
        EnrichmentJob job,
        GitHubRateLimitExceededException ex,
        CancellationToken cancellationToken)
    {
        if (job.Attempts >= settings.MaxAttempts)
        {
            await jobs.FailAsync(
                job.Id, EnrichmentErrorCategory.RateLimit,
                "GitHub rate limit exceeded repeatedly.", cancellationToken);
            return;
        }

        var nextAttempt = ex.ResetAt is { } reset && reset > DateTimeOffset.UtcNow
            ? reset
            : DateTimeOffset.UtcNow + BackoffFor(job.Attempts);
        await jobs.ScheduleRetryAsync(
            job.Id, EnrichmentErrorCategory.RateLimit, "GitHub rate limit exceeded.", nextAttempt,
            cancellationToken);
    }

    private async Task HandleTransientAsync(
        EnrichmentJob job,
        Exception ex,
        CancellationToken cancellationToken)
    {
        if (job.Attempts >= settings.MaxAttempts)
        {
            await jobs.FailAsync(
                job.Id, EnrichmentErrorCategory.Transient,
                "Transient GitHub errors exceeded retry budget.", cancellationToken);
            return;
        }

        var nextAttempt = DateTimeOffset.UtcNow + BackoffFor(job.Attempts);
        await jobs.ScheduleRetryAsync(
            job.Id, EnrichmentErrorCategory.Transient, ToSafeMessage(ex), nextAttempt,
            cancellationToken);
    }

    private TimeSpan BackoffFor(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);
        var delay = settings.TransientRetryBaseDelay * Math.Pow(2, exponent);
        return delay > settings.TransientRetryMaxDelay ? settings.TransientRetryMaxDelay : delay;
    }

    private static string ToSafeMessage(Exception ex) => ex switch
    {
        GitHubUnavailableException => "GitHub API unreachable.",
        GitHubUpstreamErrorException => "GitHub API transient server error.",
        GitHubRequestRejectedException => "GitHub rejected the request.",
        _ => "Unexpected enrichment failure.",
    };
}

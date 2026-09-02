using RepoLens.Domain.Enrichment;

namespace RepoLens.Application.Enrichment;

/// <summary>API shape returned by GET /api/repositories/{id}.</summary>
public sealed class RepositoryDetailResponse
{
    public required long Id { get; init; }
    public required long GitHubId { get; init; }
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public required string HtmlUrl { get; init; }
    public string? DefaultBranch { get; init; }
    public string? PrimaryLanguage { get; init; }
    public required int Stars { get; init; }
    public required int Forks { get; init; }
    public required int OpenIssues { get; init; }
    public required bool IsArchived { get; init; }
    public required bool IsFork { get; init; }
    public string? LicenseSpdx { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? PushedAtUtc { get; init; }
    public required DateTimeOffset DiscoveredAtUtc { get; init; }
    public DateTimeOffset? EnrichedAtUtc { get; init; }
    public required IReadOnlyList<string> Topics { get; init; }
    public required IReadOnlyList<LanguageShareDto> Languages { get; init; }
    public ReadmeInfoDto? Readme { get; init; }
    public required EnrichmentStatusDto Enrichment { get; init; }

    public sealed class LanguageShareDto
    {
        public required string Language { get; init; }
        public required long Bytes { get; init; }
        /// <summary>Share of the repository's total tracked bytes, 0–100.</summary>
        public required double Percentage { get; init; }
    }

    public sealed class ReadmeInfoDto
    {
        public required bool Present { get; init; }
        /// <summary>Normalized README text, capped for display.</summary>
        public string? Text { get; init; }
        public required bool Truncated { get; init; }
        public DateTimeOffset? FetchedAtUtc { get; init; }
    }

    public sealed class EnrichmentStatusDto
    {
        public required string Status { get; init; }
        public required int Attempts { get; init; }
        public string? LastErrorCategory { get; init; }
        public string? LastError { get; init; }
        public DateTimeOffset? NextAttemptAtUtc { get; init; }
    }
}

/// <summary>Builds the detail response from the detail reader + job status.</summary>
public sealed class RepositoryDetailService(
    IRepositoryDetailReader reader,
    IEnrichmentJobStore jobs)
{
    /// <summary>Cap for README text shipped to the UI.</summary>
    public const int ReadmeDisplayMaxLength = 60_000;

    public async Task<RepositoryDetailResponse?> GetAsync(long repositoryId, CancellationToken cancellationToken)
    {
        var source = await reader.GetAsync(repositoryId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var job = await jobs.GetLatestJobAsync(repositoryId, cancellationToken);

        var totalBytes = source.Languages.Sum(l => l.Bytes);
        var languages = source.Languages
            .OrderByDescending(l => l.Bytes)
            .Select(l => new RepositoryDetailResponse.LanguageShareDto
            {
                Language = l.Language,
                Bytes = l.Bytes,
                Percentage = totalBytes > 0 ? Math.Round(l.Bytes * 100.0 / totalBytes, 1) : 0,
            })
            .ToList();

        string? readmeText = null;
        var truncated = false;
        if (source.ReadmeText is not null)
        {
            if (source.ReadmeText.Length > ReadmeDisplayMaxLength)
            {
                readmeText = source.ReadmeText[..ReadmeDisplayMaxLength];
                truncated = true;
            }
            else
            {
                readmeText = source.ReadmeText;
            }
        }

        return new RepositoryDetailResponse
        {
            Id = source.Id,
            GitHubId = source.GitHubId,
            Owner = source.Owner,
            Name = source.Name,
            FullName = source.FullName,
            Description = source.Description,
            HtmlUrl = source.HtmlUrl,
            DefaultBranch = source.DefaultBranch,
            PrimaryLanguage = source.PrimaryLanguage,
            Stars = source.Stars,
            Forks = source.Forks,
            OpenIssues = source.OpenIssues,
            IsArchived = source.IsArchived,
            IsFork = source.IsFork,
            LicenseSpdx = source.LicenseSpdx,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
            PushedAtUtc = source.PushedAtUtc,
            DiscoveredAtUtc = source.DiscoveredAtUtc,
            EnrichedAtUtc = source.EnrichedAtUtc,
            Topics = source.Topics,
            Languages = languages,
            Readme = source.ReadmeText is null
                ? null
                : new RepositoryDetailResponse.ReadmeInfoDto
                {
                    Present = true,
                    Text = readmeText,
                    Truncated = truncated,
                    FetchedAtUtc = source.ReadmeFetchedAtUtc,
                },
            Enrichment = ToEnrichmentStatus(job),
        };
    }

    private static RepositoryDetailResponse.EnrichmentStatusDto ToEnrichmentStatus(EnrichmentJob? job)
    {
        if (job is null)
        {
            return new RepositoryDetailResponse.EnrichmentStatusDto { Status = "None", Attempts = 0 };
        }

        var statusName = job.Status switch
        {
            EnrichmentJobStatus.Pending => "Pending",
            EnrichmentJobStatus.Processing => "Processing",
            EnrichmentJobStatus.Completed => "Complete",
            EnrichmentJobStatus.Failed => "Failed",
            _ => "Unknown",
        };

        return new RepositoryDetailResponse.EnrichmentStatusDto
        {
            Status = statusName,
            Attempts = job.Attempts,
            LastErrorCategory = job.LastErrorCategory == EnrichmentErrorCategory.None
                ? null
                : job.LastErrorCategory.ToString(),
            LastError = job.LastError,
            NextAttemptAtUtc = job.NextAttemptAtUtc,
        };
    }
}

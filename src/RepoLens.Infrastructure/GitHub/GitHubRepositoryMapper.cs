using System.Globalization;

namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// Normalized repository shape produced by tolerant DTO parsing; shared by the
/// search and enrichment clients so both endpoints parse identically.
/// </summary>
internal sealed record MappedGitHubRepository(
    long GitHubId,
    string Owner,
    string Name,
    string FullName,
    string? Description,
    string HtmlUrl,
    string? DefaultBranch,
    string? PrimaryLanguage,
    int Stars,
    int Forks,
    int OpenIssues,
    bool IsArchived,
    bool IsFork,
    string? LicenseSpdx,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PushedAt,
    IReadOnlyList<string> Topics);

internal static class GitHubRepositoryMapper
{
    /// <summary>
    /// Maps one GitHub repository DTO tolerantly: identity + dates are required
    /// (missing → null), every other field degrades to a safe default. GitHub's
    /// "NOASSERTION" license sentinel becomes null.
    /// </summary>
    public static MappedGitHubRepository? TryMap(GitHubRepositoryDto dto)
    {
        if (dto.Id is not ( > 0 and var gitHubId)
            || string.IsNullOrWhiteSpace(dto.Name)
            || string.IsNullOrWhiteSpace(dto.FullName))
        {
            return null;
        }

        var owner = dto.Owner?.Login;
        if (string.IsNullOrWhiteSpace(owner))
        {
            owner = dto.FullName.Split('/', 2)[0];
        }

        var htmlUrl = string.IsNullOrWhiteSpace(dto.HtmlUrl)
            ? $"https://github.com/{dto.FullName}"
            : dto.HtmlUrl;

        if (!TryParseDate(dto.CreatedAt, out var createdAt)
            || !TryParseDate(dto.UpdatedAt, out var updatedAt))
        {
            return null;
        }

        TryParseDate(dto.PushedAt, out var pushedAt);
        var spdx = dto.License?.SpdxId;
        if (string.Equals(spdx, "NOASSERTION", StringComparison.OrdinalIgnoreCase))
        {
            spdx = null;
        }

        return new MappedGitHubRepository(
            gitHubId,
            owner,
            dto.Name,
            dto.FullName,
            string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description,
            htmlUrl,
            string.IsNullOrWhiteSpace(dto.DefaultBranch) ? null : dto.DefaultBranch,
            string.IsNullOrWhiteSpace(dto.Language) ? null : dto.Language,
            dto.StargazersCount ?? 0,
            dto.ForksCount ?? 0,
            dto.OpenIssuesCount ?? 0,
            dto.Archived ?? false,
            dto.IsFork ?? false,
            spdx,
            createdAt,
            updatedAt,
            pushedAt,
            dto.Topics ?? []);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset result)
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }
}

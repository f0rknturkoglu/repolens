using System.Text.Json.Serialization;

namespace RepoLens.Infrastructure.GitHub;

/// <summary>
/// Wire shape of GET /search/repositories (REST API v3). DTOs live inside the
/// Infrastructure adapter only and are never exposed to Domain/Application code.
/// Fields GitHub may omit are typed nullable or absent; required identity fields
/// are validated by the adapter before mapping.
/// </summary>
public sealed class GitHubSearchResponseDto
{
    [JsonPropertyName("total_count")]
    public int? TotalCount { get; set; }

    [JsonPropertyName("items")]
    public List<GitHubRepositoryDto>? Items { get; set; }
}

public sealed class GitHubRepositoryDto
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("owner")]
    public GitHubOwnerDto? Owner { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("stargazers_count")]
    public int? StargazersCount { get; set; }

    [JsonPropertyName("forks_count")]
    public int? ForksCount { get; set; }

    [JsonPropertyName("open_issues_count")]
    public int? OpenIssuesCount { get; set; }

    [JsonPropertyName("archived")]
    public bool? Archived { get; set; }

    [JsonPropertyName("fork")]
    public bool? IsFork { get; set; }

    [JsonPropertyName("license")]
    public GitHubLicenseDto? License { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public string? PushedAt { get; set; }

    [JsonPropertyName("topics")]
    public List<string>? Topics { get; set; }

    [JsonPropertyName("size")]
    public long? SizeBytes { get; set; }
}

public sealed class GitHubOwnerDto
{
    [JsonPropertyName("login")]
    public string? Login { get; set; }
}

public sealed class GitHubLicenseDto
{
    [JsonPropertyName("spdx_id")]
    public string? SpdxId { get; set; }
}

/// <summary>Rate-limit headers observed on a GitHub response.</summary>
public sealed record GitHubRateHeaders(int? Limit, int? Remaining, long? ResetUnixSeconds);

/// <summary>Error payload GitHub sends for 4xx/5xx responses.</summary>
public sealed class GitHubErrorResponseDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

using System.Text.Json;
using RepoLens.Domain.Portfolio;

namespace RepoLens.Application.Portfolio;

/// <summary>API response for portfolio analysis.</summary>
public sealed class PortfolioResponse
{
    public required long Id { get; init; }
    public required string Username { get; init; }
    public required string Version { get; init; }
    public required string Status { get; init; }
    public required int AnalyzedRepositoryCount { get; init; }
    public required int TotalRepositoryCount { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required PortfolioCoverageDto Coverage { get; init; }
    public required IReadOnlyList<SignalEvidenceDto> Signals { get; init; }

    public sealed class PortfolioCoverageDto
    {
        public required IReadOnlyList<CoverageEntryDto> Entries { get; init; }
    }

    public sealed class CoverageEntryDto
    {
        public required string Category { get; init; }
        public required int EvidenceCount { get; init; }
        public required string Band { get; init; }
        public required IReadOnlyList<string> Repositories { get; init; }
    }

    public sealed record SignalEvidenceDto(string Category, string RepositoryFullName, string Reason, int Stars);
}

/// <summary>API response for marginal-value analysis of an idea vs the portfolio.</summary>
public sealed class MarginalValueResponse
{
    public required string Username { get; init; }
    public required double Score { get; init; }
    public required string FormulaVersion { get; init; }
    public required IReadOnlyList<CategoryValueDto> Categories { get; init; }

    public sealed record CategoryValueDto(string Category, string CurrentBand, double Value, string Rationale);
}

/// <summary>Assembles portfolio snapshots into API responses.</summary>
public sealed class PortfolioResultBuilder(IPortfolioStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<PortfolioResponse?> BuildAsync(long id, CancellationToken cancellationToken)
    {
        var analysis = await store.GetAsync(id, cancellationToken);
        if (analysis is null || analysis.SignalsJson is null || analysis.CoverageJson is null)
        {
            return null;
        }

        var signals = JsonSerializer.Deserialize<List<PortfolioResponse.SignalEvidenceDto>>(analysis.SignalsJson, JsonOptions) ?? [];
        var storedEntries = JsonSerializer.Deserialize<List<StoredCoverageEntry>>(analysis.CoverageJson, JsonOptions) ?? [];

        return new PortfolioResponse
        {
            Id = analysis.Id,
            Username = analysis.Username,
            Version = analysis.Version,
            Status = analysis.Status.ToString().ToLowerInvariant(),
            AnalyzedRepositoryCount = analysis.AnalyzedRepositoryCount,
            TotalRepositoryCount = analysis.TotalRepositoryCount,
            CreatedAtUtc = analysis.CreatedAtUtc,
            CompletedAtUtc = analysis.CompletedAtUtc,
            Coverage = new PortfolioResponse.PortfolioCoverageDto
            {
                Entries = storedEntries.Select(e => new PortfolioResponse.CoverageEntryDto
                {
                    Category = e.Category,
                    EvidenceCount = e.EvidenceCount,
                    Band = e.Band,
                    Repositories = e.RepositoryFullNames,
                }).ToList(),
            },
            Signals = signals,
        };
    }

}

/// <summary>Computes marginal value against the latest stored coverage.</summary>
public sealed class MarginalValueService(IPortfolioStore store)
{
    public async Task<MarginalValueResponse> ComputeAsync(
        string username,
        string idea,
        CancellationToken cancellationToken)
    {
        var analysis = await store.GetRecentAsync(PortfolioAnalysisService.NormalizeUsername(username), cancellationToken);
        if (analysis is null || analysis.CoverageJson is null)
        {
            throw new PortfolioNotFoundException(username);
        }

        var entries = JsonSerializer.Deserialize<List<StoredCoverageEntry>>(
            analysis.CoverageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var coverageEntries = entries.Select(e => new CoverageEntry(
            e.Category, e.EvidenceCount, e.RepositoryFullNames, e.Band)).ToList();
        var coverage = new PortfolioCoverage(coverageEntries, []);

        var result = PortfolioSignals.MarginalValue(coverage, idea);
        return new MarginalValueResponse
        {
            Username = analysis.Username,
            Score = result.Score,
            FormulaVersion = result.FormulaVersion,
            Categories = result.Categories
                .Select(c => new MarginalValueResponse.CategoryValueDto(c.Category, c.CurrentBand, c.Value, c.Rationale))
                .ToList(),
        };
    }
}

/// <summary>The user has no completed portfolio analysis to compute marginal value against.</summary>
public sealed class PortfolioNotFoundException(string username) : Exception(
    $"No portfolio analysis exists for '{username}'.")
{
    public string Username { get; } = username;
}

internal sealed class StoredCoverageEntry
    {
        public string Category { get; init; } = string.Empty;
        public int EvidenceCount { get; init; }
        public List<string> RepositoryFullNames { get; init; } = [];
        public string Band { get; init; } = string.Empty;
    }

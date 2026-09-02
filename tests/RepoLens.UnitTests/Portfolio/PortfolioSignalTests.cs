using RepoLens.Application.Portfolio;

namespace RepoLens.UnitTests.Portfolio;

public sealed class PortfolioSignalTests
{
    private static PortfolioRepo Repo(
        string fullName,
        string? language = "python",
        string? description = null,
        int stars = 10,
        bool archived = false,
        bool fork = false,
        params string[] topics) => new(
        RepositoryId: fullName.GetHashCode(),
        FullName: fullName,
        Language: language,
        Description: description,
        Stars: stars,
        IsArchived: archived,
        IsFork: fork,
        SizeBytes: 0,
        PushedAtUtc: DateTimeOffset.UtcNow,
        Topics: topics);

    [Fact]
    public void Extract_LanguageMapsToCategoryWithReason()
    {
        var coverage = PortfolioSignals.Extract(
            [Repo("api-gateway", "go", "rest backend")]);

        var entry = coverage.Entries.Single(e => e.Category == "Backend");
        Assert.Equal("moderate", entry.Band);
        Assert.Equal(["api-gateway"], entry.RepositoryFullNames);
        Assert.Contains(coverage.AllEvidence, e =>
            e.Category == "Backend" && e.Reason.Contains("language", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_DeduplicatesEvidencePerRepositoryPerCategory()
    {
        // python + "machine-learning" in the description hit AI/ML twice; one item.
        var coverage = PortfolioSignals.Extract(
            [Repo("ml-models", "python", "machine-learning and ai experiments")]);

        var ai = coverage.Entries.Single(e => e.Category == "AI/ML");
        Assert.Equal(1, ai.EvidenceCount);
        Assert.Single(coverage.AllEvidence, e => e.Category == "AI/ML");
    }

    [Fact]
    public void Extract_TopicsAndDescriptionProduceCategoryHits()
    {
        var coverage = PortfolioSignals.Extract(
            [Repo("expenses", "javascript", "react frontend", topics: ["database", "sql"])]);

        Assert.Equal("moderate", coverage.Entries.Single(e => e.Category == "Frontend").Band);
        Assert.Equal("moderate", coverage.Entries.Single(e => e.Category == "Databases").Band);
        Assert.Equal("limited", coverage.Entries.Single(e => e.Category == "DevOps").Band);
    }

    [Fact]
    public void Extract_EmptyProfile_AllBandsLimited()
    {
        var coverage = PortfolioSignals.Extract([]);

        Assert.All(coverage.Entries, e => Assert.Equal("limited", e.Band));
        Assert.Empty(coverage.AllEvidence);
    }

    [Fact]
    public void Extract_ForksHeavyProfile_StillSignalsFromMetadata()
    {
        var coverage = PortfolioSignals.Extract(
        [
            Repo("fork-1", "go", fork: true),
            Repo("fork-2", "go", fork: true),
        ]);

        // Forks are analyzed like any public repo — signals come from metadata,
        // and the report shows repo-level evidence for the user to judge.
        Assert.Equal("moderate", coverage.Entries.Single(e => e.Category == "Backend").Band);
    }

    [Fact]
    public void Coverage_ThreePlusReposInCategory_IsStrong()
    {
        var repos = Enumerable.Range(1, 4)
            .Select(i => Repo($"backend-{i}", "go"))
            .ToList();

        var coverage = PortfolioSignals.Extract(repos);

        Assert.Equal("strong", coverage.Entries.Single(e => e.Category == "Backend").Band);
    }

    [Fact]
    public void MarginalValue_EmptyCategoryAddsHighValue()
    {
        var coverage = PortfolioSignals.Extract([Repo("weather", "javascript", "react charts")]);

        var result = PortfolioSignals.MarginalValue(coverage, "a postgres migration testing tool");

        Assert.Equal("marginal-v1", result.FormulaVersion);
        var databases = result.Categories.Single(c => c.Category == "Databases");
        Assert.True(databases.Value >= 0.9, "empty Database evidence should give high marginal value");
    }

    [Fact]
    public void MarginalValue_SaturatedCategoryAddsLittleValue()
    {
        var repos = Enumerable.Range(1, 5)
            .Select(i => Repo($"db-{i}", "go", "postgres migration database", topics: ["database", "postgres"]))
            .ToList();
        var coverage = PortfolioSignals.Extract(repos);

        var result = PortfolioSignals.MarginalValue(coverage, "postgres database migration tool");

        var databases = result.Categories.Single(c => c.Category == "Databases");
        Assert.True(databases.Value < 0.5, "already strong Database evidence should add little");
    }

    [Fact]
    public void MarginalValue_FallsBackToBestMatchingCategory()
    {
        var coverage = PortfolioSignals.Extract([]);

        var result = PortfolioSignals.MarginalValue(coverage, "kubernetes observability platform");

        Assert.NotEmpty(result.Categories);
        Assert.Contains(result.Categories, c => c.Category is "DevOps" or "Observability");
    }

    [Fact]
    public void UsernameValidation_RejectsInvalidInput()
    {
        Assert.NotNull(PortfolioAnalysisService.ValidateUsername(null));
        Assert.NotNull(PortfolioAnalysisService.ValidateUsername("with space"));
        Assert.NotNull(PortfolioAnalysisService.ValidateUsername("ünïcode"));
        Assert.Null(PortfolioAnalysisService.ValidateUsername("octocat"));
        Assert.Null(PortfolioAnalysisService.ValidateUsername("user-name_1"));
    }
}

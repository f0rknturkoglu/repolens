using RepoLens.Domain.Discovery;

namespace RepoLens.UnitTests.Discovery;

public sealed class RepositoryNormalizationTests
{
    private static Repository.RepositoryData SampleData(long gitHubId = 100L) => new(
        GitHubId: gitHubId,
        Owner: "migrator",
        Name: "pg-migrate",
        FullName: "migrator/pg-migrate",
        Description: null,
        HtmlUrl: "https://github.com/migrator/pg-migrate",
        DefaultBranch: null,
        PrimaryLanguage: null,
        Stars: 0,
        Forks: 0,
        OpenIssues: 0,
        IsArchived: false,
        IsFork: false,
        LicenseSpdx: null,
        CreatedAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
        PushedAt: null);

    [Fact]
    public void Create_WithMissingOptionalFields_ProducesNullsNotThrows()
    {
        var repository = Repository.Create(SampleData(), DateTimeOffset.UtcNow);

        Assert.Equal(100L, repository.GitHubId);
        Assert.Null(repository.Description);
        Assert.Null(repository.DefaultBranch);
        Assert.Null(repository.PrimaryLanguage);
        Assert.Null(repository.LicenseSpdx);
        Assert.Null(repository.PushedAt);
        Assert.Equal(0, repository.Stars);
    }

    [Fact]
    public void ApplyDiscoveredMetadata_RefreshesMutableFieldsAndKeepsId()
    {
        var repository = Repository.Create(SampleData(100L), DateTimeOffset.UtcNow);
        var fresh = SampleData(100L) with
        {
            Stars = 99,
            Description = "now described",
            LicenseSpdx = "MIT",
            PushedAt = new DateTimeOffset(2023, 5, 5, 0, 0, 0, TimeSpan.Zero),
        };

        repository.ApplyDiscoveredMetadata(fresh);

        Assert.Equal(100L, repository.GitHubId);
        Assert.Equal(99, repository.Stars);
        Assert.Equal("now described", repository.Description);
        Assert.Equal("MIT", repository.LicenseSpdx);
        Assert.NotNull(repository.PushedAt);
    }

    [Fact]
    public void ApplyDiscoveredMetadata_RefreshesDiscoveredTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var repository = Repository.Create(SampleData(1L), before.AddDays(-30));

        repository.ApplyDiscoveredMetadata(SampleData(1L));

        Assert.True(repository.DiscoveredAtUtc >= before);
    }
}

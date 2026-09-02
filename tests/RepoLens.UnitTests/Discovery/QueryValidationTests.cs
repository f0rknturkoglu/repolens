using RepoLens.Application.Discovery;

namespace RepoLens.UnitTests.Discovery;

public sealed class RepositoryDiscoveryServiceQueryValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateQuery_RejectsBlankQueries(string? query)
    {
        var errors = RepositoryDiscoveryService.ValidateQuery(query);

        Assert.NotNull(errors);
        Assert.Contains("Query is required.", errors);
    }

    [Fact]
    public void ValidateQuery_RejectsOverlongQueries()
    {
        var query = new string('x', RepositoryDiscoveryService.MaxQueryLength + 1);

        var errors = RepositoryDiscoveryService.ValidateQuery(query);

        Assert.NotNull(errors);
        Assert.Contains($"Query must be at most {RepositoryDiscoveryService.MaxQueryLength} characters.", errors);
    }

    [Fact]
    public void ValidateQuery_AcceptsAtMaxLength()
    {
        var query = new string('x', RepositoryDiscoveryService.MaxQueryLength);

        Assert.Null(RepositoryDiscoveryService.ValidateQuery(query));
    }

    [Theory]
    [InlineData("postgres migration")]
    [InlineData("language:csharp stars:>100")]
    [InlineData("elasticsearch")]
    public void ValidateQuery_AcceptsReasonableQueries(string query)
    {
        Assert.Null(RepositoryDiscoveryService.ValidateQuery(query));
    }
}

using Microsoft.Extensions.Options;
using RepoLens.Application.Discovery;
using RepoLens.Infrastructure.GitHub;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.GitHub;

/// <summary>
/// Adapter-level tests for <see cref="GitHubRepositorySearchClient"/> against a
/// WireMock GitHub API: mapping, pagination forwarding, rate-limit headers,
/// retry behavior, malformed payloads. No real GitHub dependency.
/// </summary>
public sealed class RepositorySearchClientTests
{
    private const string SamplePayload = """
        {
          "total_count": 1234,
          "incomplete_results": false,
          "items": [
            {
              "id": 101,
              "name": "pgroll",
              "full_name": "xataio/pgroll",
              "owner": { "login": "xataio" },
              "description": "PostgreSQL zero-downtime migrations",
              "html_url": "https://github.com/xataio/pgroll",
              "default_branch": "main",
              "language": "Go",
              "stargazers_count": 3456,
              "forks_count": 89,
              "open_issues_count": 12,
              "archived": false,
              "fork": false,
              "license": { "spdx_id": "Apache-2.0" },
              "created_at": "2023-02-01T10:00:00Z",
              "updated_at": "2024-06-01T10:00:00Z",
              "pushed_at": "2024-07-15T10:00:00Z"
            },
            {
              "id": 102,
              "name": "minimal-repo",
              "full_name": "someone/minimal-repo",
              "owner": { "login": "someone" },
              "html_url": "https://github.com/someone/minimal-repo",
              "created_at": "2020-01-01T00:00:00Z",
              "updated_at": "2020-02-02T00:00:00Z"
            },
            {
              "name": "orphan",
              "full_name": "who/orphan"
            }
          ]
        }
        """;

    private static GitHubRepositorySearchClient CreateClient(GitHubApiMock mock)
    {
        var http = new HttpClient();
        var options = Options.Create(new GitHubOptions { BaseUrl = mock.BaseUrl });
        return new GitHubRepositorySearchClient(http, options);
    }

    [Fact]
    public async Task Search_Success_MapsItemsSkipsUnidentifiableAndCapturesMetadata()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithHeader("x-ratelimit-limit", "30")
                .WithHeader("x-ratelimit-remaining", "28")
                .WithHeader("x-ratelimit-reset", "1700000000")
                .WithBody(SamplePayload));
        var client = CreateClient(mock);

        var result = await client.SearchAsync(
            new RepositorySearchRequest("postgres migration", 1, 30), CancellationToken.None);

        Assert.Equal(1234, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var full = result.Items[0];
        Assert.Equal(101, full.GitHubId);
        Assert.Equal("xataio", full.Owner);
        Assert.Equal("pgroll", full.Name);
        Assert.Equal("PostgreSQL zero-downtime migrations", full.Description);
        Assert.Equal("Go", full.PrimaryLanguage);
        Assert.Equal(3456, full.Stars);
        Assert.Equal(89, full.Forks);
        Assert.Equal("Apache-2.0", full.LicenseSpdx);
        Assert.False(full.IsArchived);
        Assert.Equal(new DateTimeOffset(2024, 7, 15, 10, 0, 0, TimeSpan.Zero), full.PushedAt);

        // Partial item: every omitted optional field maps to a safe default/null.
        var partial = result.Items[1];
        Assert.Equal(102, partial.GitHubId);
        Assert.Null(partial.Description);
        Assert.Null(partial.PrimaryLanguage);
        Assert.Null(partial.LicenseSpdx);
        Assert.Equal(0, partial.Stars);
        Assert.Null(partial.PushedAt);

        Assert.NotNull(result.RateLimit);
        Assert.Equal(30, result.RateLimit!.Limit);
        Assert.Equal(28, result.RateLimit.Remaining);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), result.RateLimit.ResetAt);
    }

    [Fact]
    public async Task Search_ForwardsPaginationParameters()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody("""{"total_count": 0, "items": []}"""));
        var client = CreateClient(mock);

        await client.SearchAsync(new RepositorySearchRequest("migrations", 3, 25), CancellationToken.None);

        var matches = mock.Server.FindLogEntries(
            Request.Create().WithPath("/search/repositories")
                .WithParam("page", "3")
                .WithParam("per_page", "25"));
        Assert.NotEmpty(matches); // pagination params were forwarded to GitHub
    }

    [Fact]
    public async Task Search_EmptyResult_ReturnsEmptyItems()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody("""{"total_count": 0, "items": []}"""));
        var client = CreateClient(mock);

        var result = await client.SearchAsync(
            new RepositorySearchRequest("nothing matches", 1, 30), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_RateLimited403_ThrowsRateLimitExceptionWithReset()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(403)
                .WithHeader("x-ratelimit-remaining", "0")
                .WithHeader("x-ratelimit-limit", "10")
                .WithHeader("x-ratelimit-reset", "1750000000")
                .WithBody("""{"message":"API rate limit exceeded for user"}"""));
        var client = CreateClient(mock);

        var ex = await Assert.ThrowsAsync<GitHubRateLimitExceededException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1750000000), ex.ResetAt);
    }

    [Fact]
    public async Task Search_RateLimited429_ThrowsRateLimitException()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(429).WithBody("""{"message":"Too many requests"}"""));
        var client = CreateClient(mock);

        var ex = await Assert.ThrowsAsync<GitHubRateLimitExceededException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));

        Assert.Null(ex.ResetAt);
    }

    [Fact]
    public async Task Search_Persistent5xx_RetriesThreeTimesThenThrowsUpstreamError()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("""{"message":"Server busy"}"""));
        var client = CreateClient(mock);

        var ex = await Assert.ThrowsAsync<GitHubUpstreamErrorException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(3, mock.SearchRequestCount); // initial + two bounded retries
    }

    [Fact]
    public async Task Search_Transient5xxThenSuccess_ReturnsResult()
    {
        using var mock = new GitHubApiMock();
        // First request fails with 502; WireMock scenarios make the retry succeed.
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .InScenario("retry-recovers")
            .WillSetStateTo("healthy")
            .RespondWith(Response.Create().WithStatusCode(502).WithBody("""{"message":"Bad gateway"}"""));
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .InScenario("retry-recovers")
            .WhenStateIs("healthy")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody("""{"total_count": 1, "items": [{"id": 7, "name": "ok", "full_name": "a/ok", "created_at": "2021-01-01T00:00:00Z", "updated_at": "2021-01-02T00:00:00Z"}]}"""));

        var client = CreateClient(mock);
        var result = await client.SearchAsync(
            new RepositorySearchRequest("query", 1, 30), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(7, item.GitHubId);
    }

    [Fact]
    public async Task Search_Rejected4xx_ThrowsRequestRejectedException()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithBody("""{"message":"Validation Failed","errors":[{"message":"The listed users cannot be searched either because the users do not exist"}]}"""));
        var client = CreateClient(mock);

        var ex = await Assert.ThrowsAsync<GitHubRequestRejectedException>(() =>
            client.SearchAsync(new RepositorySearchRequest("bad:qualifier", 1, 30), CancellationToken.None));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task Search_MalformedBody_ThrowsMalformedResponse()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("<html>not json</html>"));
        var client = CreateClient(mock);

        await Assert.ThrowsAsync<GitHubMalformedResponseException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));
    }

    [Fact]
    public async Task Search_MissingItemsArray_ThrowsMalformedResponse()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"total_count": 5}"""));
        var client = CreateClient(mock);

        await Assert.ThrowsAsync<GitHubMalformedResponseException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));
    }

    [Fact]
    public async Task Search_NetworkFailure_ThrowsUnavailable()
    {
        // Bind then release a port so we know nothing is listening on it.
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var closedPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var http = new HttpClient();
        var options = Options.Create(new GitHubOptions { BaseUrl = $"http://127.0.0.1:{closedPort}/" });
        var client = new GitHubRepositorySearchClient(http, options);

        await Assert.ThrowsAsync<GitHubUnavailableException>(() =>
            client.SearchAsync(new RepositorySearchRequest("query", 1, 30), CancellationToken.None));
    }
}

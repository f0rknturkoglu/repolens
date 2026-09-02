using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Api;

/// <summary>
/// End-to-end API tests for GET /api/discovery/repositories: the full flow
/// browser request → API → (mocked) GitHub → PostgreSQL → response, plus error
/// mapping for the cases the roadmap calls out. GitHub is mocked with WireMock;
/// the API runs against the real Testcontainers PostgreSQL.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class DiscoveryApiTests(PostgresContainerFixture postgres)
{
    private const string TwoItemsPayload = """
        {
          "total_count": 2,
          "items": [
            {
              "id": 9001,
              "name": "migra",
              "full_name": "davidfetter/migra",
              "owner": { "login": "davidfetter" },
              "description": "Like diff but for PostgreSQL schemas",
              "html_url": "https://github.com/davidfetter/migra",
              "default_branch": "master",
              "language": "Python",
              "stargazers_count": 3200,
              "forks_count": 210,
              "open_issues_count": 30,
              "archived": false,
              "fork": false,
              "license": { "spdx_id": "MIT" },
              "created_at": "2017-01-01T00:00:00Z",
              "updated_at": "2024-05-01T00:00:00Z",
              "pushed_at": "2024-06-01T00:00:00Z"
            },
            {
              "id": 9002,
              "name": "squabbles",
              "full_name": "acme/squabbles",
              "owner": { "login": "acme" },
              "description": null,
              "html_url": "https://github.com/acme/squabbles",
              "language": null,
              "stargazers_count": 0,
              "created_at": "2023-01-01T00:00:00Z",
              "updated_at": "2023-02-01T00:00:00Z"
            }
          ]
        }
        """;

    [Fact]
    public async Task Search_HappyPath_ReturnsNormalizedPersistedRepositories()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithHeader("x-ratelimit-limit", "30")
                .WithHeader("x-ratelimit-remaining", "27")
                .WithBody(TwoItemsPayload));

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync(
            "/api/discovery/repositories?q=postgres+migration&page=1&perPage=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DiscoveryApiResponse>();
        Assert.NotNull(body);

        Assert.Equal("postgres migration", body.Query);
        Assert.Equal(1, body.Pagination.Page);
        Assert.Equal(30, body.Pagination.PageSize);
        Assert.Equal(2, body.Pagination.TotalCount);
        Assert.Equal(2, body.Pagination.ItemCount);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal(27, body.RateLimit!.Remaining);

        var first = body.Items[0];
        Assert.Equal(9001, first.GitHubId);
        Assert.Equal("davidfetter/migra", first.FullName);
        Assert.Equal("Python", first.PrimaryLanguage);
        Assert.Equal(3200, first.Stars);
        Assert.False(first.IsArchived);
        Assert.Equal("MIT", first.LicenseSpdx);
        Assert.NotNull(first.HtmlUrl);
        Assert.True(first.Id > 0); // EF-assigned id from the canonical store

        // The discovery must have reached PostgreSQL: both rows now exist.
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;
        await using var db = new RepoLensDbContext(options);
        Assert.Equal(2, await db.Repositories.CountAsync(r => r.GitHubId == 9001 || r.GitHubId == 9002));
        Assert.Equal(3200, (await db.Repositories.SingleAsync(r => r.GitHubId == 9001)).Stars);
    }

    [Theory]
    [InlineData("/api/discovery/repositories?page=1")]
    [InlineData("/api/discovery/repositories?q=")]
    [InlineData("/api/discovery/repositories?q=%20%20")]
    public async Task Search_MissingOrBlankQuery_Returns400(string path)
    {
        using var mock = new GitHubApiMock();
        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, mock.SearchRequestCount); // no upstream call for invalid input
    }

    [Fact]
    public async Task Search_GitHubRateLimited_Returns429WithoutLeakingRawGitHubMessage()
    {
        var resetUnix = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(403)
                .WithHeader("x-ratelimit-remaining", "0")
                .WithHeader("x-ratelimit-limit", "10")
                .WithHeader("x-ratelimit-reset", resetUnix.ToString())
                .WithBody("""{"message":"API rate limit exceeded for SUPER-SECRET-CONTEXT"}"""));

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/discovery/repositories?q=postgres+migration");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = Assert.IsType<TimeSpan>(response.Headers.RetryAfter?.Delta);
        Assert.InRange(retryAfter.TotalSeconds, 3000, 3700); // ~1h, minus processing time
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SUPER-SECRET-CONTEXT", text);
        Assert.Contains("github_rate_limited", text);
    }

    [Fact]
    public async Task Search_GitHub5xxExhausted_Returns502UpstreamError()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("""{"message":"boom"}"""));

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/discovery/repositories?q=postgres+migration");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("upstream_error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_GitHubRejectsQuery_Returns400InvalidQuery()
    {
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithBody("""{"message":"Validation Failed"}"""));

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/discovery/repositories?q=user%3Adoesnotexist12345xyz");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_query", await response.Content.ReadAsStringAsync());
    }

    private sealed class DiscoveryApiResponse
    {
        public string Query { get; init; } = string.Empty;
        public PageMetadataJson Pagination { get; init; } = new();
        public RateLimitJson? RateLimit { get; init; }
        public List<RepositoryItemJson> Items { get; init; } = [];

        public sealed class PageMetadataJson
        {
            public int Page { get; init; }
            public int PageSize { get; init; }
            public int? TotalCount { get; init; }
            public int ItemCount { get; init; }
        }

        public sealed class RateLimitJson
        {
            public int Limit { get; init; }
            public int Remaining { get; init; }
        }

        public sealed class RepositoryItemJson
        {
            public long Id { get; init; }
            public long GitHubId { get; init; }
            public string FullName { get; init; } = string.Empty;
            public string? Description { get; init; }
            public string HtmlUrl { get; init; } = string.Empty;
            public string? PrimaryLanguage { get; init; }
            public int Stars { get; init; }
            public bool IsArchived { get; init; }
            public string? LicenseSpdx { get; init; }
        }
    }
}

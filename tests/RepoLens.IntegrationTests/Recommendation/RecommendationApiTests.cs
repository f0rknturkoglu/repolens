using System.Net;
using System.Net.Http.Json;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Recommendation;

/// <summary>
/// Recommendation API tests: deterministic top-3 (distinct categories) with
/// explainability, request cache (no second GitHub round trip), and validation.
/// No LLM is configured — the deterministic candidate bank runs.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class RecommendationApiTests(PostgresContainerFixture postgres)
{
    private static string RepoJson(long id, string name) => $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "owner-x/{{name}}",
          "owner": { "login": "owner-x" },
          "html_url": "https://github.com/owner-x/{{name}}",
          "description": "related developer tooling",
          "language": "Go",
          "stargazers_count": 20,
          "forks_count": 1,
          "open_issues_count": 1,
          "archived": false,
          "fork": false,
          "created_at": "2022-01-01T00:00:00Z",
          "updated_at": "2024-01-01T00:00:00Z",
          "pushed_at": "2024-06-01T00:00:00Z"
        }
        """;

    private static void MockAnySearch(GitHubApiMock mock, params long[] ids)
    {
        var items = string.Join(',', ids.Select(id => RepoJson(id, $"related-tool-{id}")));
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody($$"""{"total_count": {{ids.Length}}, "items": [{{items}}]}"""));
    }

    [Fact]
    public async Task Recommend_ReturnsThreeDistinctExplainedProjects()
    {
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 99001, 99002, 99003);

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/recommendations", new
        {
            goal = "Build developer tools around databases",
            interests = new[] { "databases", "testing" },
            durationWeeks = "6",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecommendationJson>();
        Assert.NotNull(body);
        Assert.Equal("completed", body.Status);
        Assert.False(body.ServedFromCache);
        Assert.Equal(3, body.Items.Count);
        // Diversity: distinct categories.
        Assert.Equal(3, body.Items.Select(i => i.Category).Distinct().Count());
        // Explainability: every item explains why it ranked where it did.
        Assert.All(body.Items, i =>
        {
            Assert.NotEmpty(i.WhyRankedHere);
            Assert.InRange(i.Score, 0, 1);
        });
    }

    [Fact]
    public async Task Recommend_IdenticalRequestHitsCache()
    {
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 99010);

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();
        var payload = new
        {
            goal = "database testing tools",
            interests = new[] { "databases" },
        };

        var first = await client.PostAsJsonAsync("/api/recommendations", payload);
        var firstBody = await first.Content.ReadFromJsonAsync<RecommendationJson>();
        var searchesAfterFirst = mock.SearchRequestCount;
        Assert.True(searchesAfterFirst > 0);

        using var second = await client.PostAsJsonAsync("/api/recommendations", payload);
        var secondBody = await second.Content.ReadFromJsonAsync<RecommendationJson>();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(secondBody);
        Assert.True(secondBody.ServedFromCache);
        Assert.Equal(firstBody!.Id, secondBody.Id);
        Assert.Equal(searchesAfterFirst, mock.SearchRequestCount);
    }

    [Fact]
    public async Task Recommend_MissingGoal_Returns400()
    {
        using var mock = new GitHubApiMock();
        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/recommendations", new { goal = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recommend_GetById_ReplaysReport()
    {
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 99020);
        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        var created = await client.PostAsJsonAsync("/api/recommendations", new
        {
            goal = "testing infrastructure",
            interests = new[] { "devops" },
        });
        var createdBody = await created.Content.ReadFromJsonAsync<RecommendationJson>();

        using var replay = await client.GetAsync($"/api/recommendations/{createdBody!.Id}");

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<RecommendationJson>();
        Assert.NotNull(replayed);
        Assert.Equal(createdBody.Items.Count, replayed.Items.Count);
    }

    private sealed class RecommendationJson
    {
        public long Id { get; init; }
        public string Version { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool ServedFromCache { get; init; }
        public List<ItemJson> Items { get; init; } = [];

        public sealed class ItemJson
        {
            public string Title { get; init; } = string.Empty;
            public string Category { get; init; } = string.Empty;
            public double Score { get; init; }
            public List<string> WhyRankedHere { get; init; } = [];
            public EvidenceJson Evidence { get; init; } = new();
        }

        public sealed class EvidenceJson
        {
            public LandscapeJson Landscape { get; init; } = new();
        }

        public sealed class LandscapeJson
        {
            public List<string>? Competitors { get; init; }
        }
    }
}

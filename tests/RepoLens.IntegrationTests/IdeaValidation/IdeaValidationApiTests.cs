using System.Net;
using System.Net.Http.Json;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.IdeaValidation;

/// <summary>
/// Idea validation API tests: end-to-end validation against mocked GitHub,
/// deterministic fallback when the LLM returns malformed output, and the
/// PostgreSQL-backed cache (identical idea → no second GitHub call).
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class IdeaValidationApiTests(PostgresContainerFixture postgres)
{
    private static string RepoJson(long id, string name, string language = "Go") => $$"""
        {
          "id": {{id}},
          "name": "{{name}}",
          "full_name": "owner-x/{{name}}",
          "owner": { "login": "owner-x" },
          "html_url": "https://github.com/owner-x/{{name}}",
          "description": "postgres migration tooling",
          "language": "{{language}}",
          "stargazers_count": 40,
          "forks_count": 2,
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
        var items = string.Join(',', ids.Select(id => RepoJson(id, $"pg-migration-tool-{id}")));
        mock.Server.Given(Request.Create().WithPath("/search/repositories").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody($$"""{"total_count": {{ids.Length}}, "items": [{{items}}]}"""));
    }

    private const string Idea = "postgres migration load testing";

    [Fact]
    public async Task Validate_CompletesWithNoveltyCompetitorsAndEvidence()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 97001, 97002);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = Idea });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IdeaResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("completed", body.Status);
        Assert.False(body.ServedFromCache);
        Assert.NotNull(body.Novelty);
        Assert.InRange(body.Novelty.Score, 0, 100);
        Assert.Equal(2, body.Novelty.CandidateCount);
        Assert.Equal("limited", body.Novelty.EvidenceSufficiency);
        Assert.NotEmpty(body.Novelty.Components);
        Assert.Equal(100, body.Novelty.Components.Sum(c => c.WeightPercent));
        Assert.Equal("novelty-v1", body.Novelty.FormulaVersion);
        Assert.NotNull(body.QueryPlan);
        Assert.NotEmpty(body.QueryPlan);
        Assert.All(body.QueryPlan, q => Assert.Equal("fallback", q.Source)); // no LLM configured
        Assert.NotNull(body.Evidence);
        Assert.NotEmpty(body.Limitations);
    }

    [Fact]
    public async Task Validate_IdenticalIdeaHitsCacheWithoutNewGitHubCalls()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 97010);
        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        var first = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = Idea });
        var firstBody = await first.Content.ReadFromJsonAsync<IdeaResponseJson>();
        var callsAfterFirst = mock.SearchRequestCount;
        Assert.True(callsAfterFirst > 0);

        using var second = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = Idea });
        var secondBody = await second.Content.ReadFromJsonAsync<IdeaResponseJson>();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(secondBody);
        Assert.True(secondBody.ServedFromCache);
        Assert.Equal(firstBody!.Id, secondBody.Id);
        Assert.Equal(callsAfterFirst, mock.SearchRequestCount); // cache: no second GitHub round trip
    }

    [Fact]
    public async Task Validate_MalformedLlmOutput_FallsBackToDeterministicPlan()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        MockAnySearch(mock, 97020);
        // A chat endpoint that returns non-JSON content: the plan builder must
        // validate the output and fall back deterministically.
        mock.Server.Given(Request.Create().WithPath("/chat/completions").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody("""{"choices":[{"message":{"content":"I cannot help with that."}}]}"""));

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web =>
            {
                web.UseSetting("GitHub:BaseUrl", mock.BaseUrl);
                web.UseSetting("Llm:BaseUrl", mock.BaseUrl);
                web.UseSetting("Llm:Model", "garbage-model");
                web.UseSetting("Llm:ApiKey", "test");
            });
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = Idea });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IdeaResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("completed", body.Status);
        Assert.NotNull(body.QueryPlan);
        Assert.NotEmpty(body.QueryPlan);
        Assert.All(body.QueryPlan, q => Assert.Equal("fallback", q.Source));
    }

    [Fact]
    public async Task Validate_BlankIdea_Returns400()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Validate_NoCandidates_ExposesInsufficientEvidenceAlongsideHighNovelty()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        MockAnySearch(mock);

        await using var application = new ApiApplication(
            postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/analysis/idea", new { idea = "unrepresented test concept" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IdeaResponseJson>();
        Assert.NotNull(body?.Novelty);
        Assert.Equal(100, body.Novelty.Score);
        Assert.Equal(0, body.Novelty.CandidateCount);
        Assert.Equal("insufficient", body.Novelty.EvidenceSufficiency);
        Assert.Contains(body.Novelty.Components, component => component.Key == "candidates");
    }

    private sealed class IdeaResponseJson
    {
        public long Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool ServedFromCache { get; init; }
        public NoveltyJson? Novelty { get; init; }
        public List<PlanQueryJson>? QueryPlan { get; init; }
        public List<string>? Evidence { get; init; }
        public List<string> Limitations { get; init; } = [];

        public sealed class NoveltyJson
        {
            public double Score { get; init; }
            public string FormulaVersion { get; init; } = string.Empty;
            public int CandidateCount { get; init; }
            public string EvidenceSufficiency { get; init; } = string.Empty;
            public List<ComponentJson> Components { get; init; } = [];
        }

        public sealed class ComponentJson
        {
            public string Key { get; init; } = string.Empty;
            public string Label { get; init; } = string.Empty;
            public double Value { get; init; }
            public string Evidence { get; init; } = string.Empty;
            public int WeightPercent { get; init; }
        }

        public sealed record PlanQueryJson(string Text, string Source);
    }
}

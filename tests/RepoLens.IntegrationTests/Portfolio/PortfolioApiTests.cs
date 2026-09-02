using System.Net;
using System.Net.Http.Json;
using RepoLens.IntegrationTests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace RepoLens.IntegrationTests.Portfolio;

/// <summary>
/// Portfolio API tests against mocked GitHub user listings: coverage report,
/// marginal-value analysis, and the not-found path. PostgreSQL is real.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class PortfolioApiTests(PostgresContainerFixture postgres)
{
    private const string RepoItem = """
        {
          "id": __ID__,
          "name": "__NAME__",
          "full_name": "octocat/__NAME__",
          "owner": { "login": "octocat" },
          "html_url": "https://github.com/octocat/__NAME__",
          "description": "__DESC__",
          "language": "__LANG__",
          "stargazers_count": __STARS__,
          "forks_count": 1,
          "open_issues_count": 1,
          "archived": false,
          "fork": false,
          "topics": __TOPICS__,
          "size": 1024,
          "created_at": "2021-01-01T00:00:00Z",
          "updated_at": "2024-01-01T00:00:00Z",
          "pushed_at": "2024-06-01T00:00:00Z"
        }
        """;

    private static string Repo(long id, string name, string description, string language, int stars, string topicsJson) =>
        RepoItem
            .Replace("__ID__", id.ToString())
            .Replace("__NAME__", name)
            .Replace("__DESC__", description)
            .Replace("__LANG__", language)
            .Replace("__STARS__", stars.ToString())
            .Replace("__TOPICS__", topicsJson);

    private static void MockUserRepos(GitHubApiMock mock, string body)
    {
        mock.Server.Given(Request.Create().WithPath("/users/octocat/repos").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("content-type", "application/json")
                .WithBody(body));
    }

    [Fact]
    public async Task Analyze_ProducesCoverageAndEvidenceReport()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        var body = "[" +
            Repo(98001, "migra-clone", "postgres schema migration tool", "Go", 300, """["migration","postgres","database"]""") + "," +
            Repo(98002, "weather-ui", "react weather charts", "JavaScript", 50, """["react","charts"]""") + "," +
            Repo(98003, "ml-lab", "machine learning experiments", "Python", 80, """["machine-learning","ai"]""") +
            "]";
        MockUserRepos(mock, body);

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/portfolio/octocat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PortfolioJson>();
        Assert.NotNull(result);
        Assert.Equal("completed", result.Status);
        Assert.Equal(3, result.TotalRepositoryCount);
        Assert.Equal(3, result.AnalyzedRepositoryCount);

        var databases = result.Coverage.Entries.Single(e => e.Category == "Databases");
        Assert.Equal("moderate", databases.Band);
        Assert.Contains("octocat/migra-clone", databases.Repositories);
        Assert.Contains(result.Signals, s => s.Category == "AI/ML" && s.RepositoryFullName == "octocat/ml-lab");
    }

    [Fact]
    public async Task Marginal_EmptyCategoryIdeaReturnsHighMarginalValue()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        MockUserRepos(mock, "["
            + Repo(98010, "weather-ui", "react weather charts", "JavaScript", 50, """["react"]""")
            + "]");

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/portfolio/octocat/marginal",
            new { idea = "postgres database migration testing" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MarginalJson>();
        Assert.NotNull(result);
        Assert.Equal("marginal-v1", result.FormulaVersion);
        var databases = result.Categories.Single(c => c.Category == "Databases");
        Assert.True(databases.Value >= 0.9);
    }

    [Fact]
    public async Task Analyze_UnknownUser_Returns404WithoutLeakingGitHubText()
    {
        await using var _wipe = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        using var mock = new GitHubApiMock();
        mock.Server.Given(Request.Create().WithPath("/users/ghost/repos").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404)
                .WithBody("""{"message":"Not Found SECRET"}"""));

        await using var application = new ApiApplication(
                    postgres.ConnectionString,
            web => web.UseSetting("GitHub:BaseUrl", mock.BaseUrl));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/portfolio/ghost");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET", text);
        Assert.Contains("not_found", text);
    }

    private sealed class PortfolioJson
    {
        public string Status { get; init; } = string.Empty;
        public int TotalRepositoryCount { get; init; }
        public int AnalyzedRepositoryCount { get; init; }
        public string? TaxonomyVersionHint { get; init; }
        public CoverageJson Coverage { get; init; } = new();
        public List<SignalJson> Signals { get; init; } = [];

        public sealed class CoverageJson
        {
            public List<EntryJson> Entries { get; init; } = [];
        }

        public sealed class EntryJson
        {
            public string Category { get; init; } = string.Empty;
            public string Band { get; init; } = string.Empty;
            public List<string> Repositories { get; init; } = [];
        }

        public sealed class SignalJson
        {
            public string Category { get; init; } = string.Empty;
            public string RepositoryFullName { get; init; } = string.Empty;
            public string Reason { get; init; } = string.Empty;
            public int Stars { get; init; }
        }
    }

    private sealed class MarginalJson
    {
        public string FormulaVersion { get; init; } = string.Empty;
        public List<CategoryJson> Categories { get; init; } = [];

        public sealed class CategoryJson
        {
            public string Category { get; init; } = string.Empty;
            public string CurrentBand { get; init; } = string.Empty;
            public double Value { get; init; }
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Api;

/// <summary>
/// API tests for repository detail + manual refresh. Persistence-backed only —
/// no GitHub dependency, because neither endpoint calls GitHub.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class RepositoryDetailApiTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task GetRepositoryDetail_ReturnsRepositoryWithEmptyEnrichment()
    {
        await using var db = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        var repository = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, 93001);

        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var response = await client.GetAsync($"/api/repositories/{repository.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DetailResponse>();
        Assert.NotNull(body);
        Assert.Equal("owner-x/repo-93001", body.FullName);
        Assert.Equal("None", body.Enrichment.Status);
        Assert.Empty(body.Topics);
        Assert.Empty(body.Languages);
        Assert.Null(body.Readme);
    }

    [Fact]
    public async Task GetRepositoryDetail_UnknownId_Returns404()
    {
        await using var db = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/repositories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_FirstCallAccepted_SecondCallConflicts()
    {
        await using var db = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        var repository = await Enrichment.EnrichmentTestData.InsertRepositoryAsync(db, 93002);

        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var first = await client.PostAsync($"/api/repositories/{repository.Id}/refresh", null);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Second refresh finds the pending job → conflict (no duplicate jobs).
        using var second = await client.PostAsync($"/api/repositories/{repository.Id}/refresh", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Exactly one job was created.
        Assert.Equal(1, await db.EnrichmentJobs.CountAsync(j => j.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Refresh_UnknownRepository_Returns404()
    {
        await using var db = await Enrichment.EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        await using var application = new ApiApplication(postgres.ConnectionString);
                using var client = application.CreateClient();

        using var response = await client.PostAsync("/api/repositories/999998/refresh", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class DetailResponse
    {
        public string FullName { get; init; } = string.Empty;
        public List<string> Topics { get; init; } = [];
        public List<object> Languages { get; init; } = [];
        public object? Readme { get; init; }
        public EnrichmentJson Enrichment { get; init; } = new();

        public sealed class EnrichmentJson
        {
            public string Status { get; init; } = string.Empty;
        }
    }
}

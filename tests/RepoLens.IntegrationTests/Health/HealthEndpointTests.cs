using System.Net;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Health;

[Collection(PostgresContainerFixture.CollectionName)]
public sealed class HealthEndpointTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task GetHealth_Returns200_WhenApiAndDatabaseAreReachable()
    {
        await using var application = new ApiApplication(postgres.ConnectionString);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

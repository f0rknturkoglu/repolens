using System.Net;
using System.Net.Http.Json;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Auth;

/// <summary>
/// Auth endpoints without OAuth configuration: the feature reports disabled and
/// protected endpoints reject anonymous callers with 401 (not 500).
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class AuthEndpointsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Status_WithoutOAuthConfiguration_ReportsDisabled()
    {
        await using var application = new ApiApplication(postgres.ConnectionString);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StatusJson>();
        Assert.NotNull(body);
        Assert.False(body.Enabled);
        Assert.Null(body.User);
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401()
    {
        await using var application = new ApiApplication(postgres.ConnectionString);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task History_WithoutSession_Returns401()
    {
        await using var application = new ApiApplication(postgres.ConnectionString);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/auth/me/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutOAuthConfiguration_Returns404()
    {
        await using var application = new ApiApplication(postgres.ConnectionString);
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/api/auth/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class StatusJson
    {
        public bool Enabled { get; init; }
        public object? User { get; init; }
    }
}

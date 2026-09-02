using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RepoLens.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real RepoLens.Api entry point (WebApplicationFactory) pointed at a
/// caller-provided connection string. Exercises the same Program.cs wiring that
/// runs in production — EF Core configuration, health checks, logging,
/// OpenTelemetry, migrations. <paramref name="configureWebHost"/> lets tests
/// override configuration (e.g. the GitHub API base URL for WireMock).
/// </summary>
public sealed class ApiApplication(
    string connectionString,
    Action<IWebHostBuilder>? configureWebHost = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Skip appsettings.Development.json: the container connection string is the
        // single source of truth for the database in tests.
        builder.UseEnvironment("IntegrationTests");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        configureWebHost?.Invoke(builder);
    }
}

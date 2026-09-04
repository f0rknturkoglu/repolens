using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        var knownKeys = new[]
        {
            "ConnectionStrings:DefaultConnection",
            "GitHub:BaseUrl",
            "Embedding:BaseUrl",
            "Embedding:Model",
            "Embedding:ApiKey",
            "Llm:BaseUrl",
            "Llm:Model",
            "Llm:ApiKey"
        };

        var overrides = new Dictionary<string, string?>();
        foreach (var key in knownKeys)
        {
            if (builder.GetSetting(key) is { } value)
            {
                overrides[key] = value;
            }
        }

        builder.ConfigureServices(services =>
        {
            services.ConfigureHttpClientDefaults(b =>
            {
                b.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseProxy = false
                });
            });

            services.PostConfigure<RepoLens.Infrastructure.Embeddings.EmbeddingOptions>(opt =>
            {
                if (overrides.TryGetValue("Embedding:BaseUrl", out var b) && b != null) opt.BaseUrl = b;
                if (overrides.TryGetValue("Embedding:Model", out var m) && m != null) opt.Model = m;
                if (overrides.TryGetValue("Embedding:ApiKey", out var k)) opt.ApiKey = k;
            });
            services.PostConfigure<RepoLens.Infrastructure.GitHub.GitHubOptions>(opt =>
            {
                if (overrides.TryGetValue("GitHub:BaseUrl", out var g) && g != null) opt.BaseUrl = g;
            });
            services.PostConfigure<RepoLens.Application.Ai.LlmSettings>(opt =>
            {
                if (overrides.TryGetValue("Llm:BaseUrl", out var l) && l != null) opt.BaseUrl = l;
                if (overrides.TryGetValue("Llm:Model", out var lm) && lm != null) opt.Model = lm;
                if (overrides.TryGetValue("Llm:ApiKey", out var lk)) opt.ApiKey = lk;
            });
        });
    }
}

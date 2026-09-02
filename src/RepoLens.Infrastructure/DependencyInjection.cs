using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RepoLens.Application.Enrichment;
using RepoLens.Application.Discovery;
using RepoLens.Infrastructure.Content;
using RepoLens.Infrastructure.GitHub;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure;

/// <summary>
/// Registers infrastructure services: the EF Core DbContext bound to the single
/// PostgreSQL instance, GitHub API adapters (optional token, bounded retries,
/// typed errors), enrichment content normalization, and the EF implementations
/// of Application ports.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RepoLensDbContext>(options => options.UseNpgsql(connectionString));

        services.AddOptions<GitHubOptions>()
            .BindConfiguration(GitHubOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "GitHub:BaseUrl must be configured.");

        services.AddHttpClient<IGitHubRepositorySearchClient, GitHubRepositorySearchClient>(
            (sp, client) => ConfigureGitHubClient(sp, client));
        services.AddHttpClient<IGitHubRepositoryClient, GitHubRepositoryClient>(
            (sp, client) => ConfigureGitHubClient(sp, client));

        services.AddSingleton<ITextNormalizer, MarkdigTextNormalizer>();

        services.AddScoped<IRepositoryStore, RepositoryStore>();
        services.AddScoped<IRepositoryDetailReader>(sp => sp.GetRequiredService<RepositoryStore>());
        services.AddScoped<IEnrichmentJobStore, EnrichmentJobStore>();

        return services;
    }

    private static void ConfigureGitHubClient(IServiceProvider sp, HttpClient client)
    {
        var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RepoLens.Application.Discovery;
using RepoLens.Infrastructure.GitHub;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure;

/// <summary>
/// Registers infrastructure services: the EF Core DbContext bound to the single
/// PostgreSQL instance, the GitHub API HTTP adapter (optional token, bounded
/// retries, typed errors), and the EF implementations of Application ports.
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
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
            });

        services.AddScoped<IRepositoryStore, RepositoryStore>();

        return services;
    }
}

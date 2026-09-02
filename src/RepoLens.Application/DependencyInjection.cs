using Microsoft.Extensions.DependencyInjection;
using RepoLens.Application.Discovery;
using RepoLens.Application.Enrichment;

namespace RepoLens.Application;

/// <summary>
/// Registers Application-layer use cases. No EF or HTTP knowledge here.
/// <paramref name="enrichmentSettings"/> carries the policy knobs bound from
/// configuration by the composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        EnrichmentSettings enrichmentSettings)
    {
        services.AddScoped<RepositoryDiscoveryService>();
        services.AddScoped<RepositoryEnrichmentProcessor>();
        services.AddScoped<RepositoryEnrichmentScheduler>();
        services.AddScoped<RepositoryDetailService>();
        services.AddSingleton(enrichmentSettings);

        return services;
    }
}

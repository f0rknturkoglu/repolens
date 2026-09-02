using Microsoft.Extensions.DependencyInjection;
using RepoLens.Application.Discovery;

namespace RepoLens.Application;

/// <summary>Registers Application-layer use cases. No EF or HTTP knowledge here.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RepositoryDiscoveryService>();

        return services;
    }
}

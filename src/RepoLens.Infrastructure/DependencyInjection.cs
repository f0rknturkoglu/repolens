using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services. Currently: the EF Core DbContext bound
    /// to the single PostgreSQL instance used by the modular monolith.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RepoLensDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}

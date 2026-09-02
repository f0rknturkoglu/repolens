using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// Design-time context factory for `dotnet ef` tooling. Migrations are authored
/// against a placeholder connection string; the runtime connection comes from
/// configuration (see Program.cs), never from this factory.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RepoLensDbContext>
{
    public RepoLensDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql("Host=localhost;Database=repolens;Username=repolens;Password=repolens_dev_password")
            .Options;

        return new RepoLensDbContext(options);
    }
}

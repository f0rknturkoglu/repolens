using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Discovery;
using RepoLens.Infrastructure.Persistence.Configurations;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// RepoLens relational store (PostgreSQL + pgvector). Domain entities and their
/// EF Core mappings are registered here as features land (Discovery, Analysis,
/// Recommendation, Portfolio).
/// </summary>
public sealed class RepoLensDbContext(DbContextOptions<RepoLensDbContext> options) : DbContext(options)
{
    public DbSet<Repository> Repositories => Set<Repository>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepoLensDbContext).Assembly);
    }
}

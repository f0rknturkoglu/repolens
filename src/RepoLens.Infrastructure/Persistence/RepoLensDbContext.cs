using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Discovery;
using RepoLens.Domain.Enrichment;
using RepoLens.Domain.Searching;
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
    public DbSet<EnrichmentJob> EnrichmentJobs => Set<EnrichmentJob>();
    public DbSet<RepositoryTopic> RepositoryTopics => Set<RepositoryTopic>();
    public DbSet<RepositoryLanguage> RepositoryLanguages => Set<RepositoryLanguage>();
    public DbSet<RepositoryReadme> RepositoryReadmes => Set<RepositoryReadme>();
    public DbSet<RepositorySnapshot> RepositorySnapshots => Set<RepositorySnapshot>();
    public DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepoLensDbContext).Assembly);
    }
}

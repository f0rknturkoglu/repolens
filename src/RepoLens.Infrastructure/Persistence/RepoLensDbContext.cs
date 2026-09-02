using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Analysis;
using RepoLens.Domain.Discovery;
using RepoLens.Domain.Analysis;
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
    public DbSet<EcosystemAnalysis> EcosystemAnalyses => Set<EcosystemAnalysis>();
    public DbSet<EcosystemAnalysisCandidate> EcosystemAnalysisCandidates => Set<EcosystemAnalysisCandidate>();
    public DbSet<EcosystemCluster> EcosystemClusters => Set<EcosystemCluster>();
    public DbSet<IdeaValidation> IdeaValidations => Set<IdeaValidation>();
    public DbSet<Domain.Portfolio.PortfolioAnalysis> PortfolioAnalyses => Set<Domain.Portfolio.PortfolioAnalysis>();
    public DbSet<Domain.Recommendation.RecommendationRequest> RecommendationRequests => Set<Domain.Recommendation.RecommendationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepoLensDbContext).Assembly);
    }
}

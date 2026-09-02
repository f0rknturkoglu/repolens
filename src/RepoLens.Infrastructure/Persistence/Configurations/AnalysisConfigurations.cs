using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Analysis;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public sealed class PortfolioAnalysisConfiguration : IEntityTypeConfiguration<Domain.Portfolio.PortfolioAnalysis>
{
    public void Configure(EntityTypeBuilder<Domain.Portfolio.PortfolioAnalysis> builder)
    {
        builder.ToTable("portfolio_analyses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Version).HasColumnName("version").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(a => a.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(a => a.AnalyzedRepositoryCount).HasColumnName("analyzed_repository_count").IsRequired();
        builder.Property(a => a.TotalRepositoryCount).HasColumnName("total_repository_count").IsRequired();
        builder.Property(a => a.SignalsJson).HasColumnName("signals_json").HasColumnType("jsonb");
        builder.Property(a => a.CoverageJson).HasColumnName("coverage_json").HasColumnType("jsonb");

        builder.HasIndex(a => new { a.Username, a.CreatedAtUtc });
    }
}

public sealed class EcosystemAnalysisConfiguration : IEntityTypeConfiguration<EcosystemAnalysis>
{
    public void Configure(EntityTypeBuilder<EcosystemAnalysis> builder)
    {
        builder.ToTable("ecosystem_analyses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Query).HasColumnName("query").HasMaxLength(300).IsRequired();
        builder.Property(a => a.Version).HasColumnName("version").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.Error).HasColumnName("error").IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(a => a.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(a => a.MetricsJson).HasColumnName("metrics_json").HasColumnType("jsonb");
        builder.Property(a => a.VariantsJson).HasColumnName("variants_json").HasColumnType("jsonb");

        builder.HasIndex(a => new { a.Query, a.CreatedAtUtc });
    }
}

public sealed class EcosystemAnalysisCandidateConfiguration : IEntityTypeConfiguration<EcosystemAnalysisCandidate>
{
    public void Configure(EntityTypeBuilder<EcosystemAnalysisCandidate> builder)
    {
        builder.ToTable("ecosystem_analysis_candidates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(c => c.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(c => c.QueryVariant).HasColumnName("query_variant").HasMaxLength(32).IsRequired();
        builder.Property(c => c.RankInVariant).HasColumnName("rank_in_variant").IsRequired();

        builder.HasIndex(c => new { c.AnalysisId, c.RepositoryId }).IsUnique();
        builder.HasIndex(c => c.RepositoryId);
    }
}

public sealed class EcosystemClusterConfiguration : IEntityTypeConfiguration<EcosystemCluster>
{
    public void Configure(EntityTypeBuilder<EcosystemCluster> builder)
    {
        builder.ToTable("ecosystem_clusters");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(c => c.Label).HasColumnName("label").HasMaxLength(128).IsRequired();
        builder.Property(c => c.MembersJson).HasColumnName("members_json").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(c => c.AnalysisId);
    }
}

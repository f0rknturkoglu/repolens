using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Analysis;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public sealed class IdeaValidationConfiguration : IEntityTypeConfiguration<IdeaValidation>
{
    public void Configure(EntityTypeBuilder<IdeaValidation> builder)
    {
        builder.ToTable("idea_validations");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.IdeaText).HasColumnName("idea_text").HasColumnType("text").IsRequired();
        builder.Property(v => v.IdeaHash).HasColumnName("idea_hash").HasMaxLength(64).IsRequired();
        builder.Property(v => v.Version).HasColumnName("version").HasMaxLength(16).IsRequired();
        builder.Property(v => v.NoveltyFormula).HasColumnName("novelty_formula").HasMaxLength(32).IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").IsRequired();
        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(v => v.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(v => v.SearchPlanJson).HasColumnName("search_plan_json").HasColumnType("jsonb");
        builder.Property(v => v.MetricsJson).HasColumnName("metrics_json").HasColumnType("jsonb");
        builder.Property(v => v.ClustersJson).HasColumnName("clusters_json").HasColumnType("jsonb");
        builder.Property(v => v.CompetitorsJson).HasColumnName("competitors_json").HasColumnType("jsonb");
        builder.Property(v => v.NoveltyJson).HasColumnName("novelty_json").HasColumnType("jsonb");
        builder.Property(v => v.GapsJson).HasColumnName("gaps_json").HasColumnType("jsonb");

        builder.HasIndex(v => new { v.IdeaHash, v.CreatedAtUtc });
        builder.HasIndex(v => v.Status);
    }
}

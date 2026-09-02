using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Recommendation;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public sealed class RecommendationRequestConfiguration : IEntityTypeConfiguration<RecommendationRequest>
{
    public void Configure(EntityTypeBuilder<RecommendationRequest> builder)
    {
        builder.ToTable("recommendation_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Goal).HasColumnName("goal").HasColumnType("text").IsRequired();
        builder.Property(r => r.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Username).HasColumnName("username").HasMaxLength(64);
        builder.Property(r => r.InterestsJson).HasColumnName("interests_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ConstraintsJson).HasColumnName("constraints_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.Version).HasColumnName("version").HasMaxLength(16).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(r => r.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(r => r.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");

        builder.HasIndex(r => new { r.RequestHash, r.CreatedAtUtc });
    }
}

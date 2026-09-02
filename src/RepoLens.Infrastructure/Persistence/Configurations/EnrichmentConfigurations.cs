using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Enrichment;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public sealed class EnrichmentJobConfiguration : IEntityTypeConfiguration<EnrichmentJob>
{
    public void Configure(EntityTypeBuilder<EnrichmentJob> builder)
    {
        builder.ToTable("enrichment_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");
        builder.Property(j => j.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(j => j.Type).HasColumnName("type").IsRequired();
        builder.Property(j => j.Status).HasColumnName("status").IsRequired();
        builder.Property(j => j.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(j => j.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(j => j.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(j => j.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(j => j.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(j => j.LastErrorCategory).HasColumnName("last_error_category").IsRequired();
        builder.Property(j => j.LastError).HasColumnName("last_error").HasMaxLength(500);

        builder.HasIndex(j => new { j.RepositoryId, j.Status });
        builder.HasIndex(j => j.Status);
    }
}

public sealed class RepositoryTopicConfiguration : IEntityTypeConfiguration<RepositoryTopic>
{
    public void Configure(EntityTypeBuilder<RepositoryTopic> builder)
    {
        builder.ToTable("repository_topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(t => t.Topic).HasColumnName("topic").HasMaxLength(128).IsRequired();
        builder.Property(t => t.AddedAtUtc).HasColumnName("added_at_utc").IsRequired();

        builder.HasIndex(t => new { t.RepositoryId, t.Topic }).IsUnique();
    }
}

public sealed class RepositoryLanguageConfiguration : IEntityTypeConfiguration<RepositoryLanguage>
{
    public void Configure(EntityTypeBuilder<RepositoryLanguage> builder)
    {
        builder.ToTable("repository_languages");
        builder.HasKey(l => new { l.RepositoryId, l.Language });
        builder.Property(l => l.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(l => l.Language).HasColumnName("language").HasMaxLength(128).IsRequired();
        builder.Property(l => l.Bytes).HasColumnName("bytes").IsRequired();
        builder.Property(l => l.CapturedAtUtc).HasColumnName("captured_at_utc").IsRequired();
    }
}

public sealed class RepositoryReadmeConfiguration : IEntityTypeConfiguration<RepositoryReadme>
{
    public void Configure(EntityTypeBuilder<RepositoryReadme> builder)
    {
        builder.ToTable("repository_readmes");
        builder.HasKey(r => r.RepositoryId);
        builder.Property(r => r.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(r => r.RawContent).HasColumnName("raw_content").HasColumnType("text").IsRequired();
        builder.Property(r => r.TextContent).HasColumnName("text_content").HasColumnType("text").IsRequired();
        builder.Property(r => r.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.FetchedAtUtc).HasColumnName("fetched_at_utc").IsRequired();

        builder.HasIndex(r => r.ContentHash);
    }
}

public sealed class RepositorySnapshotConfiguration : IEntityTypeConfiguration<RepositorySnapshot>
{
    public void Configure(EntityTypeBuilder<RepositorySnapshot> builder)
    {
        builder.ToTable("repository_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(s => s.Stars).HasColumnName("stars").IsRequired();
        builder.Property(s => s.Forks).HasColumnName("forks").IsRequired();
        builder.Property(s => s.OpenIssues).HasColumnName("open_issues").IsRequired();
        builder.Property(s => s.CapturedAtUtc).HasColumnName("captured_at_utc").IsRequired();

        builder.HasIndex(s => new { s.RepositoryId, s.CapturedAtUtc });
    }
}

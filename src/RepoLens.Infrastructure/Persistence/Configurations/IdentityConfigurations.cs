using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Identity;

namespace RepoLens.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.GitHubId).HasColumnName("github_id").IsRequired();
        builder.Property(u => u.Login).HasColumnName("login").HasMaxLength(64).IsRequired();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(1024);
        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(u => u.LastLoginAtUtc).HasColumnName("last_login_at_utc").IsRequired();

        builder.HasIndex(u => u.GitHubId).IsUnique();
    }
}

public sealed class SavedAnalysisConfiguration : IEntityTypeConfiguration<SavedAnalysis>
{
    public void Configure(EntityTypeBuilder<SavedAnalysis> builder)
    {
        builder.ToTable("saved_analyses");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Kind).HasColumnName("kind").HasMaxLength(24).IsRequired();
        builder.Property(s => s.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(s => s.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(s => s.Version).HasColumnName("version").HasMaxLength(16).IsRequired();
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(s => new { s.UserId, s.CreatedAtUtc });
    }
}

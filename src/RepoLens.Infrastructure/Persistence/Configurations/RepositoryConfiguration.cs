using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Discovery;

namespace RepoLens.Infrastructure.Persistence.Configurations;

/// <summary>
/// Repository mapping: identity is the EF primary key; GitHub's numeric id is a
/// unique business key and full name a unique secondary natural key, so repeated
/// discovery can never create duplicates.
/// </summary>
public sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("repositories");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.GitHubId).HasColumnName("github_id").IsRequired();
        builder.Property(r => r.Owner).HasColumnName("owner").HasMaxLength(255).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(r => r.FullName).HasColumnName("full_name").HasMaxLength(512).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(r => r.HtmlUrl).HasColumnName("html_url").HasMaxLength(1024).IsRequired();
        builder.Property(r => r.DefaultBranch).HasColumnName("default_branch").HasMaxLength(255);
        builder.Property(r => r.PrimaryLanguage).HasColumnName("primary_language").HasMaxLength(64);
        builder.Property(r => r.Stars).HasColumnName("stars").IsRequired();
        builder.Property(r => r.Forks).HasColumnName("forks").IsRequired();
        builder.Property(r => r.OpenIssues).HasColumnName("open_issues").IsRequired();
        builder.Property(r => r.IsArchived).HasColumnName("is_archived").IsRequired();
        builder.Property(r => r.IsFork).HasColumnName("is_fork").IsRequired();
        builder.Property(r => r.LicenseSpdx).HasColumnName("license_spdx").HasMaxLength(64);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.PushedAt).HasColumnName("pushed_at");
        builder.Property(r => r.DiscoveredAtUtc).HasColumnName("discovered_at_utc").IsRequired();
        builder.Property(r => r.EnrichedAtUtc).HasColumnName("enriched_at_utc");

        builder.HasIndex(r => r.GitHubId).IsUnique();
        builder.HasIndex(r => r.FullName).IsUnique();
    }
}

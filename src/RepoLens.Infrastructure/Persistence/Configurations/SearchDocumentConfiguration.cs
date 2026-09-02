using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepoLens.Domain.Searching;

namespace RepoLens.Infrastructure.Persistence.Configurations;

/// <summary>
/// Search document mapping. The tsvector column (generated from the text
/// columns) and the pgvector embedding column are managed by raw SQL in
/// migrations and <see cref="Persistence.RepositorySearchStore"/> — no EF type
/// mapping exists for them, so they are intentionally absent here.
/// </summary>
public sealed class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        builder.ToTable("search_documents");
        builder.HasKey(d => d.RepositoryId);
        builder.Property(d => d.RepositoryId).HasColumnName("repository_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(d => d.Topics).HasColumnName("topics").HasColumnType("text");
        builder.Property(d => d.PrimaryLanguage).HasColumnName("primary_language").HasMaxLength(64);
        builder.Property(d => d.ReadmeText).HasColumnName("readme_text").HasColumnType("text");
        builder.Property(d => d.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(d => d.EmbeddingHash).HasColumnName("embedding_hash").HasMaxLength(64);
        builder.Property(d => d.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(128);
        builder.Property(d => d.EmbeddingCreatedAtUtc).HasColumnName("embedding_created_at_utc");

        builder.HasIndex(d => d.ContentHash);
        builder.HasIndex(d => new { d.EmbeddingHash, d.EmbeddingModel });
    }
}

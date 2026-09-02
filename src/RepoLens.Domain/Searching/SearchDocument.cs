using System.Security.Cryptography;
using System.Text;

namespace RepoLens.Domain.Searching;

/// <summary>
/// One canonical search document per enriched repository: the text PostgreSQL
/// full-text search indexes (weighted: name A, topics B, description C, README
/// D), plus the content hash that decides whether reindexing/embedding is
/// needed. The embedding vector itself lives in the same row (managed via raw
/// SQL because pgvector has no Npgsql/EF mapping yet); this entity tracks its
/// staleness metadata only.
/// </summary>
public sealed class SearchDocument
{
    private SearchDocument()
    {
        // EF Core materialization.
        Name = string.Empty;
        ContentHash = string.Empty;
    }

    private SearchDocument(
        long repositoryId,
        string name,
        string? description,
        string? topics,
        string? primaryLanguage,
        string? readmeText,
        string contentHash)
    {
        RepositoryId = repositoryId;
        Name = name;
        Description = description;
        Topics = topics;
        PrimaryLanguage = primaryLanguage;
        ReadmeText = readmeText;
        ContentHash = contentHash;
    }

    public long RepositoryId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Space-joined lowercased topics; lexed with weight B.</summary>
    public string? Topics { get; private set; }

    public string? PrimaryLanguage { get; private set; }

    /// <summary>Normalized README text; lexed with weight D.</summary>
    public string? ReadmeText { get; private set; }

    /// <summary>SHA-256 over the canonical document text (all fields).</summary>
    public string ContentHash { get; private set; }

    /// <summary>Content hash the current embedding was generated for; stale when it differs from ContentHash.</summary>
    public string? EmbeddingHash { get; private set; }

    /// <summary>Embedding model/provider identifier the vector came from.</summary>
    public string? EmbeddingModel { get; private set; }

    public DateTimeOffset? EmbeddingCreatedAtUtc { get; private set; }

    /// <summary>Embryo document; embedding metadata is applied later via raw SQL.</summary>
    public static SearchDocument Create(
        long repositoryId,
        string name,
        string? description,
        string? topics,
        string? primaryLanguage,
        string? readmeText,
        string contentHash) =>
        new(repositoryId, name, description, topics, primaryLanguage, readmeText, contentHash);

    /// <summary>Replaces the text content (content changed upstream). Embedding metadata is left
    /// untouched — the embedding hash now disagrees, marking the embedding stale.</summary>
    public void ReplaceContent(
        string name,
        string? description,
        string? topics,
        string? primaryLanguage,
        string? readmeText,
        string contentHash)
    {
        Name = name;
        Description = description;
        Topics = topics;
        PrimaryLanguage = primaryLanguage;
        ReadmeText = readmeText;
        ContentHash = contentHash;
    }

    public bool IsEmbeddingStale(string currentEmbeddingModel) =>
        !string.Equals(EmbeddingHash, ContentHash, StringComparison.Ordinal)
        || !string.Equals(EmbeddingModel, currentEmbeddingModel, StringComparison.Ordinal);

    public void ApplyEmbeddingMetadata(string? model, string? hash, DateTimeOffset? createdAtUtc)
    {
        EmbeddingModel = model;
        EmbeddingHash = hash;
        EmbeddingCreatedAtUtc = createdAtUtc;
    }

    public static string Hash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

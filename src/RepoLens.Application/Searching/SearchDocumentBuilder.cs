using RepoLens.Application.Enrichment;
using RepoLens.Domain.Searching;

namespace RepoLens.Application.Searching;

/// <summary>Builds the canonical search-document text from enriched repository data.</summary>
public static class SearchDocumentBuilder
{
    /// <summary>README characters fed into the embedding input (FTS still indexes the full text).</summary>
    public const int EmbeddingReadmeMaxLength = 8_000;

    public static SearchDocument Build(RepositoryDetailSource source)
    {
        var topics = string.Join(' ', source.Topics.OrderBy(t => t, StringComparer.Ordinal));
        var canonical = CanonicalText(
            source.Name, source.Description, topics, source.PrimaryLanguage, source.ReadmeText);

        return SearchDocument.Create(
            source.Id,
            source.Name,
            source.Description,
            topics.Length > 0 ? topics : null,
            source.PrimaryLanguage,
            source.ReadmeText,
            SearchDocument.Hash(canonical));
    }

    /// <summary>Text embedded for semantic search (README capped).</summary>
    public static string EmbeddingText(SearchDocument document)
    {
        var readme = document.ReadmeText is { Length: > 0 } r
            ? r[..Math.Min(r.Length, EmbeddingReadmeMaxLength)]
            : string.Empty;
        return CanonicalText(document.Name, document.Description, document.Topics, document.PrimaryLanguage, readme);
    }

    private static string CanonicalText(
        string name,
        string? description,
        string? topics,
        string? primaryLanguage,
        string? readmeText) =>
        string.Join('\n', name, description ?? string.Empty, topics ?? string.Empty,
            primaryLanguage ?? string.Empty, readmeText ?? string.Empty);
}

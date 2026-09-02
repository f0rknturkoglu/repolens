using Microsoft.EntityFrameworkCore;
using Npgsql;
using RepoLens.Application.Searching;
using RepoLens.Domain.Searching;
using RepoLens.Infrastructure.Persistence;
using RepoLens.Infrastructure.Searching;

namespace RepoLens.Infrastructure.Persistence;

/// <summary>
/// EF + raw-SQL implementation of <see cref="IRepositorySearchStore"/>. Text
/// documents go through EF; the pgvector embedding column and the tsvector-based
/// retrieval legs are raw SQL (no EF type mapping for vector/tsvector).
/// </summary>
public sealed class RepositorySearchStore(RepoLensDbContext db) : IRepositorySearchStore
{
    private sealed record LegRow(long RepositoryId, double Score);

    public async Task SaveDocumentAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        var existing = await db.SearchDocuments
            .SingleOrDefaultAsync(d => d.RepositoryId == document.RepositoryId, cancellationToken);
        if (existing is null)
        {
            db.SearchDocuments.Add(document);
        }
        else
        {
            existing.ReplaceContent(
                document.Name,
                document.Description,
                document.Topics,
                document.PrimaryLanguage,
                document.ReadmeText,
                document.ContentHash);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveEmbeddingAsync(
        long repositoryId,
        float[]? embedding,
        string? model,
        string? embeddingHash,
        DateTimeOffset? createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (embedding is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE search_documents
                 SET embedding = NULL, embedding_model = NULL, embedding_hash = NULL,
                     embedding_created_at_utc = NULL
                 WHERE repository_id = {repositoryId}
                 """,
                cancellationToken);
            return;
        }

        var literal = VectorText.ToLiteral(embedding);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE search_documents
             SET embedding = CAST({literal} AS vector),
                 embedding_model = {model},
                 embedding_hash = {embeddingHash},
                 embedding_created_at_utc = {createdAtUtc}
             WHERE repository_id = {repositoryId}
             """,
            cancellationToken);
    }

    public Task<SearchDocument?> GetSearchDocumentAsync(long repositoryId, CancellationToken cancellationToken) =>
        db.SearchDocuments.SingleOrDefaultAsync(d => d.RepositoryId == repositoryId, cancellationToken);

    public async Task<(float[] Vector, string Model)?> ReadEmbeddingVectorAsync(
        long repositoryId,
        CancellationToken cancellationToken)
    {
        var row = await db.SearchDocuments
            .Where(d => d.RepositoryId == repositoryId)
            .Select(d => new { d.EmbeddingModel })
            .SingleOrDefaultAsync(cancellationToken);
        if (row?.EmbeddingModel is null)
        {
            return null;
        }

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT embedding::text FROM search_documents WHERE repository_id = @id";
        command.Parameters.Add(new NpgsqlParameter("id", repositoryId));
        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string literal)
        {
            return null;
        }

        return (VectorText.FromLiteral(literal), row.EmbeddingModel);
    }

    public async Task<IReadOnlyList<long>> ListStaleEmbeddingDocumentIdsAsync(
        string currentModel,
        int limit,
        CancellationToken cancellationToken) =>
        await db.SearchDocuments
            .Where(d => d.EmbeddingHash == null
                        || d.EmbeddingHash != d.ContentHash
                        || d.EmbeddingModel != currentModel)
            .OrderBy(d => d.RepositoryId)
            .Select(d => d.RepositoryId)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<SearchLegResult> KeywordSearchAsync(
        string query,
        SearchFilters filters,
        int take,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT sd.repository_id AS "RepositoryId",
                   ts_rank_cd(sd.search_vector, plainto_tsquery('english', @q), '{0.1,0.2,0.4,1.0}') AS "Score"
            FROM search_documents sd
            JOIN repositories r ON r.id = sd.repository_id
            WHERE sd.search_vector @@ plainto_tsquery('english', @q)
              AND (@language IS NULL OR r.primary_language = @language)
              AND (@archived IS NULL OR r.is_archived = @archived)
              AND r.stars >= @minStars
            ORDER BY "Score" DESC, sd.repository_id
            LIMIT @take
            """;
        var rows = await db.Database.SqlQueryRaw<LegRow>(
            sql,
            new NpgsqlParameter("q", query),
            new NpgsqlParameter("language", filters.Language),
            new NpgsqlParameter("archived", filters.Archived),
            new NpgsqlParameter("minStars", filters.MinStars ?? 0),
            new NpgsqlParameter("take", take)).ToListAsync(cancellationToken);

        return new SearchLegResult(
            rows.Select(r => r.RepositoryId).ToList(),
            rows.Select(r => r.Score).ToList());
    }

    public async Task<SearchLegResult> VectorSearchAsync(
        float[] queryVector,
        string model,
        SearchFilters filters,
        int take,
        CancellationToken cancellationToken)
    {
        var literal = VectorText.ToLiteral(queryVector);
        var sql = $"""
            SELECT sd.repository_id AS "RepositoryId",
                   (sd.embedding <=> CAST(@q AS vector))::float8 AS "Score"
            FROM search_documents sd
            JOIN repositories r ON r.id = sd.repository_id
            WHERE sd.embedding IS NOT NULL
              AND sd.embedding_model = @model
              AND (@language IS NULL OR r.primary_language = @language)
              AND (@archived IS NULL OR r.is_archived = @archived)
              AND r.stars >= @minStars
            ORDER BY "Score" ASC, sd.repository_id
            LIMIT @take
            """;
        var rows = await db.Database.SqlQueryRaw<LegRow>(
            sql,
            new NpgsqlParameter("q", literal),
            new NpgsqlParameter("model", model),
            new NpgsqlParameter("language", filters.Language),
            new NpgsqlParameter("archived", filters.Archived),
            new NpgsqlParameter("minStars", filters.MinStars ?? 0),
            new NpgsqlParameter("take", take)).ToListAsync(cancellationToken);

        return new SearchLegResult(
            rows.Select(r => r.RepositoryId).ToList(),
            rows.Select(r => r.Score).ToList());
    }

    public async Task<IReadOnlyList<SearchRepositorySource>> GetRepositoriesAsync(
        IReadOnlyList<long> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return [];
        }

        return await db.Repositories
            .Where(r => repositoryIds.Contains(r.Id))
            .Select(r => new SearchRepositorySource
            {
                Id = r.Id,
                GitHubId = r.GitHubId,
                Owner = r.Owner,
                Name = r.Name,
                FullName = r.FullName,
                Description = r.Description,
                HtmlUrl = r.HtmlUrl,
                DefaultBranch = r.DefaultBranch,
                PrimaryLanguage = r.PrimaryLanguage,
                Stars = r.Stars,
                Forks = r.Forks,
                OpenIssues = r.OpenIssues,
                IsArchived = r.IsArchived,
                IsFork = r.IsFork,
                LicenseSpdx = r.LicenseSpdx,
                CreatedAtUtc = r.CreatedAt,
                UpdatedAtUtc = r.UpdatedAt,
                PushedAtUtc = r.PushedAt,
                Topics = new List<string>(),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SearchRepositorySource?> GetRepositoryWithTopicsAsync(
        long repositoryId,
        CancellationToken cancellationToken)
    {
        var repository = await db.Repositories
            .SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            return null;
        }

        var topics = await db.RepositoryTopics
            .Where(t => t.RepositoryId == repositoryId)
            .OrderBy(t => t.Topic)
            .Select(t => t.Topic)
            .ToListAsync(cancellationToken);

        return new SearchRepositorySource
        {
            Id = repository.Id,
            GitHubId = repository.GitHubId,
            Owner = repository.Owner,
            Name = repository.Name,
            FullName = repository.FullName,
            Description = repository.Description,
            HtmlUrl = repository.HtmlUrl,
            DefaultBranch = repository.DefaultBranch,
            PrimaryLanguage = repository.PrimaryLanguage,
            Stars = repository.Stars,
            Forks = repository.Forks,
            OpenIssues = repository.OpenIssues,
            IsArchived = repository.IsArchived,
            IsFork = repository.IsFork,
            LicenseSpdx = repository.LicenseSpdx,
            CreatedAtUtc = repository.CreatedAt,
            UpdatedAtUtc = repository.UpdatedAt,
            PushedAtUtc = repository.PushedAt,
            Topics = topics,
        };
    }
}

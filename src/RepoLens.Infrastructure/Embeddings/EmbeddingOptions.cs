namespace RepoLens.Infrastructure.Embeddings;

/// <summary>
/// Configuration for the embedding provider. Model and endpoint come from
/// configuration ("Embedding" section / environment variables) — never from
/// code. The API key is optional (local/Ollama-style endpoints need none).
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
}

namespace RepoLens.Application.Ai;

/// <summary>One structured LLM completion request/response pair (JSON contract).</summary>
public sealed record LlmJsonRequest(string SystemPrompt, string UserPrompt);

public sealed record LlmJsonResponse(string Json, string Model);

/// <summary>
/// Port for LLM completions with a strict JSON contract. The provider and model
/// come from configuration; callers validate and fall back deterministically —
/// RepoLens never lets unparsed LLM output reach its pipeline.
/// </summary>
public interface ILlmClient
{
    /// <summary>Configured model identifier.</summary>
    string Model { get; }

    /// <summary>True when a provider/model is configured at all.</summary>
    bool IsConfigured { get; }

    Task<LlmJsonResponse> CompleteJsonAsync(LlmJsonRequest request, CancellationToken cancellationToken);
}

/// <summary>Raised when the LLM cannot be reached or returns an unreadable response.</summary>
public sealed class LlmUnavailableException(Exception? inner) : Exception(
    "The configured LLM provider is unavailable.", inner);

/// <summary>
/// LLM configuration knobs ("Llm" section). The model is configuration, never
/// code; the API key is optional and supplied via environment variables.
/// </summary>
public sealed class LlmSettings
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "";
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

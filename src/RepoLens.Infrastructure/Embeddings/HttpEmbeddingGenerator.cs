using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Searching;

namespace RepoLens.Infrastructure.Embeddings;

/// <summary>
/// OpenAI-compatible embeddings provider (POST {base}/embeddings). Works against
/// OpenAI, Azure OpenAI-compatible endpoints, Ollama, etc. — the only contract
/// is the wire format. Errors degrade to <see cref="EmbeddingUnavailableException"/>
/// so callers can fall back to keyword search.
/// </summary>
public sealed class HttpEmbeddingGenerator(
    HttpClient http,
    IOptions<EmbeddingOptions> options) : IEmbeddingGenerator
{
    public string Model => options.Value.Model ?? string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.Model)
        && !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public async Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new EmbeddingUnavailableException(null);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(options.Value.BaseUrl.TrimEnd('/') + "/embeddings", UriKind.Absolute))
            {
                Content = JsonContent.Create(new
                {
                    model = options.Value.Model,
                    input = texts,
                }),
            };

            if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            {
                request.Headers.Authorization = new("Bearer", options.Value.ApiKey);
            }

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new EmbeddingUnavailableException(null);
            }

            var body = await response.Content.ReadFromJsonAsync<EmbeddingsResponseDto>(
                EmbeddingJsonOptions, cancellationToken);
            if (body?.Data is null)
            {
                throw new EmbeddingUnavailableException(null);
            }

            var ordered = body.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding ?? throw new EmbeddingUnavailableException(null))
                .ToList();
            if (ordered.Count != texts.Count)
            {
                throw new EmbeddingUnavailableException(null);
            }

            return ordered;
        }
        catch (HttpRequestException ex)
        {
            throw new EmbeddingUnavailableException(ex);
        }
        catch (JsonException ex)
        {
            throw new EmbeddingUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmbeddingUnavailableException(ex);
        }
    }

    private static readonly JsonSerializerOptions EmbeddingJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class EmbeddingsResponseDto
    {
        public List<EmbeddingDataDto>? Data { get; set; }
    }

    private sealed class EmbeddingDataDto
    {
        public int Index { get; set; }
        public float[]? Embedding { get; set; }
    }
}

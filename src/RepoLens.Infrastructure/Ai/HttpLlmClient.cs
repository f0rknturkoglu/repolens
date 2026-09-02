using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Ai;

namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// OpenAI-compatible chat-completions client (POST {base}/chat/completions).
/// The response text is passed through verbatim; structured-output validation is
/// the caller's job. Any transport/status/parse failure raises
/// <see cref="LlmUnavailableException"/> so the pipeline falls back.
/// </summary>
public sealed class HttpLlmClient(
    HttpClient http,
    IOptions<LlmSettings> options) : ILlmClient
{
    public string Model => options.Value.Model ?? string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.Model)
        && !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public async Task<LlmJsonResponse> CompleteJsonAsync(
        LlmJsonRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new LlmUnavailableException(null);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(options.Value.BaseUrl.TrimEnd('/') + "/chat/completions", UriKind.Absolute))
            {
                Content = JsonContent.Create(new
                {
                    model = options.Value.Model,
                    messages = new[]
                    {
                        new { role = "system", content = request.SystemPrompt },
                        new { role = "user", content = request.UserPrompt },
                    },
                    temperature = 0,
                    response_format = new { type = "json_object" },
                }),
            };

            if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            {
                httpRequest.Headers.Authorization = new("Bearer", options.Value.ApiKey);
            }

            using var response = await http.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmUnavailableException(null);
            }

            var body = await response.Content.ReadFromJsonAsync<ChatResponseDto>(
                JsonOptions, cancellationToken);
            var text = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new LlmUnavailableException(null);
            }

            return new LlmJsonResponse(text, options.Value.Model);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException(ex);
        }
        catch (JsonException ex)
        {
            throw new LlmUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmUnavailableException(ex);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class ChatResponseDto
    {
        public List<ChoiceDto>? Choices { get; set; }
    }

    private sealed class ChoiceDto
    {
        public MessageDto? Message { get; set; }
    }

    private sealed class MessageDto
    {
        public string? Content { get; set; }
    }
}

using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepoLens.Application.Identity;

namespace RepoLens.Infrastructure.Identity;

/// <summary>
/// GitHub OAuth exchange (code → access token → /user). Client id/secret come
/// from configuration/environment; raw GitHub data never leaves this adapter.
/// </summary>
public sealed class GitHubOAuthClient(
    HttpClient http,
    IOptions<GitHubOAuthSettings> settings,
    IOptions<Infrastructure.GitHub.GitHubOptions> gitHubOptions) : IGitHubOAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<GitHubOAuthUser> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var s = settings.Value;
        if (!s.IsEnabled)
        {
            throw new InvalidOperationException("GitHub OAuth is not configured.");
        }

        // Token exchange.
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
            new Uri(gitHubOptions.Value.BaseUrl.TrimEnd('/') + "/login/oauth/access_token", UriKind.Absolute))
        {
            Content = JsonContent.Create(new
            {
                client_id = s.ClientId,
                client_secret = s.ClientSecret,
                code,
            }),
        };
        tokenRequest.Headers.Accept.ParseAdd("application/json");
        using var tokenResponse = await http.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<TokenDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GitHub token response was unreadable.");

        // User fetch with the access token.
        using var userRequest = new HttpRequestMessage(HttpMethod.Get,
            new Uri(gitHubOptions.Value.BaseUrl.TrimEnd('/') + "/user", UriKind.Absolute));
        userRequest.Headers.Authorization = new("Bearer", tokenBody.AccessToken);
        using var userResponse = await http.SendAsync(userRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();
        var user = await userResponse.Content.ReadFromJsonAsync<UserDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GitHub user response was unreadable.");

        return new GitHubOAuthUser(
            user.Id ?? throw new InvalidOperationException("GitHub user response had no id."),
            user.Login ?? string.Empty,
            user.Name,
            user.AvatarUrl);
    }

    private sealed class TokenDto
    {
        public string? AccessToken { get; set; }
    }

    private sealed class UserDto
    {
        public long? Id { get; set; }
        public string? Login { get; set; }
        public string? Name { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
    }
}

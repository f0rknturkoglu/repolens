using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Api;

[Collection(PostgresContainerFixture.CollectionName)]
public sealed class RateLimitingProxyTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task TrustedForwardedClients_IgnoreSpoofedPrefixesAndUseSeparatePartitions()
    {
        await using var application = CreateApplication(trustForwardedHeaders: true);
        using var client = application.CreateClient();

        for (var requestNumber = 0; requestNumber < 20; requestNumber++)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                await SendInvalidExpensiveRequestAsync(
                    client,
                    $"192.0.2.{requestNumber + 1}, 198.51.100.10"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            await SendInvalidExpensiveRequestAsync(client, "203.0.113.99, 198.51.100.10"));
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendInvalidExpensiveRequestAsync(client, "192.0.2.99, 203.0.113.20"));
    }

    [Fact]
    public async Task UntrustedForwardedHeader_DoesNotChangeClientPartition()
    {
        await using var application = CreateApplication(trustForwardedHeaders: false);
        using var client = application.CreateClient();

        for (var requestNumber = 0; requestNumber < 20; requestNumber++)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                await SendInvalidExpensiveRequestAsync(client, "198.51.100.10"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            await SendInvalidExpensiveRequestAsync(client, "203.0.113.20"));
    }

    private ApiApplication CreateApplication(bool trustForwardedHeaders) =>
        new(postgres.ConnectionString, web =>
            web.UseSetting("ReverseProxy:TrustForwardedHeaders", trustForwardedHeaders.ToString()));

    private static async Task<HttpStatusCode> SendInvalidExpensiveRequestAsync(
        HttpClient client,
        string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/analysis/idea")
        {
            Content = JsonContent.Create(new { idea = " " })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

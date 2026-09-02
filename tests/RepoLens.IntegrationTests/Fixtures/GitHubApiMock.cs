using WireMock.Server;

namespace RepoLens.IntegrationTests.Fixtures;

/// <summary>
/// A WireMock.Net server pre-wired for the GitHub REST API repository-search
/// endpoint. Each test creates its own instance so mocks never leak between
/// tests; requests run against a random local port.
/// </summary>
public sealed class GitHubApiMock : IDisposable
{
    public GitHubApiMock()
    {
        Server = WireMockServer.Start();
    }

    public WireMockServer Server { get; }

    public string BaseUrl => Server.Url!;

    public int SearchRequestCount => Server.LogEntries.Count(e =>
        e.RequestMessage?.AbsolutePath.Contains("/search/repositories", StringComparison.Ordinal) == true);

    public void Dispose() => Server.Dispose();
}

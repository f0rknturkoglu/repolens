using System.Net;
using System.Runtime.CompilerServices;

namespace RepoLens.IntegrationTests;

internal static class TestAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Integration tests run against local WireMock servers.
        // Prevent system proxies (e.g. macOS scutil 127.0.0.1:8080) from intercepting loopback requests.
        HttpClient.DefaultProxy = new WebProxy();
    }
}

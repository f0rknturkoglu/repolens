using Testcontainers.PostgreSql;

namespace RepoLens.IntegrationTests.Fixtures;

/// <summary>
/// One real PostgreSQL container with pgvector, shared by every test in the
/// "postgres" collection. Tests that need external HTTP mocking add a WireMock.Net
/// server alongside this container (no sample tests yet — the dependency is wired
/// so future GitHub API tests can mock without new infrastructure).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string CollectionName = "postgres";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("repolens_tests")
        .WithUsername("repolens")
        .WithPassword("repolens_test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(PostgresContainerFixture.CollectionName)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;

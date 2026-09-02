using System.Globalization;
using Npgsql;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Database;

/// <summary>
/// Proves the PostgreSQL image the project standardizes on ships a usable
/// pgvector extension: it can be enabled and its operators execute.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class PgVectorExtensionTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task VectorExtension_CanBeEnabledAndUsed()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using (var enableExtension = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection))
        {
            await enableExtension.ExecuteNonQueryAsync();
        }

        await using (var extensionVersion = new NpgsqlCommand(
            "SELECT extversion FROM pg_extension WHERE extname = 'vector'", connection))
        {
            var version = await extensionVersion.ExecuteScalarAsync();
            Assert.False(string.IsNullOrWhiteSpace(version as string),
                "pg_extension should report a version for 'vector' after CREATE EXTENSION.");
        }

        // L2 distance between identical vectors must be 0; a wrong/missing
        // extension would fail this query outright.
        await using (var distanceQuery = new NpgsqlCommand(
            "SELECT '[1,2,3]'::vector <-> '[1,2,3]'::vector", connection))
        {
            var distance = Convert.ToDouble(
                await distanceQuery.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(0.0, distance, precision: 6);
        }
    }
}

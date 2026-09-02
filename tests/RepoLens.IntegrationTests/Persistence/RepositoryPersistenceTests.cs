using Microsoft.EntityFrameworkCore;
using Npgsql;
using RepoLens.Domain.Discovery;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Persistence;

/// <summary>
/// Repository persistence against a real PostgreSQL container: upsert by GitHub
/// id (through the real RepositoryStore), metadata refresh on repeated
/// discovery, and unique-constraint behavior. The table is wiped before each
/// test because the container DB is shared.
/// </summary>
[Collection(PostgresContainerFixture.CollectionName)]
public sealed class RepositoryPersistenceTests(PostgresContainerFixture postgres)
{
    private async Task<RepoLensDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;
        var db = new RepoLensDbContext(options);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM repositories");
        return db;
    }

    private static Repository RepositoryWith(
        long gitHubId,
        string fullName,
        int stars = 1,
        string? description = "d") => Repository.Create(
            new Repository.RepositoryData(
                gitHubId,
                fullName.Split('/', 2)[0],
                fullName.Split('/', 2)[1],
                fullName,
                description,
                $"https://github.com/{fullName}",
                "main",
                "C#",
                stars,
                0,
                0,
                false,
                false,
                "MIT",
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
                null),
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task InsertNewRepository_PersistsWithAssignedId()
    {
        await using var db = await CreateContextAsync();

        var repository = RepositoryWith(5001, "owner/new-repo");
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();

        Assert.True(repository.Id > 0);

        var stored = await db.Repositories.SingleAsync(r => r.GitHubId == 5001);
        Assert.Equal("owner/new-repo", stored.FullName);
        Assert.Equal("C#", stored.PrimaryLanguage);
        Assert.Equal("MIT", stored.LicenseSpdx);
    }

    [Fact]
    public async Task Upsert_SameGitHubId_UpdatesMetadataWithoutCreatingDuplicate()
    {
        await using var db = await CreateContextAsync();
        var store = new RepositoryStore(db);
        var first = RepositoryWith(5002, "owner/dup-repo", stars: 10, description: "original");
        await store.UpsertAsync([first], CancellationToken.None);

        // A second discovery of the SAME GitHub repository (a new entity, as the
        // store sees it on the next search) must update, never duplicate.
        var second = RepositoryWith(5002, "owner/dup-repo", stars: 25, description: "updated");
        await store.UpsertAsync([second], CancellationToken.None);

        Assert.Equal(1, await db.Repositories.CountAsync());
        var stored = await db.Repositories.SingleAsync();
        Assert.Equal(25, stored.Stars);
        Assert.Equal("updated", stored.Description);
    }

    [Fact]
    public async Task Upsert_DifferentGitHubIdsWithSameFullName_IsPreventedByUniqueIndex()
    {
        await using var db = await CreateContextAsync();
        var first = RepositoryWith(5003, "owner/name-conflict");
        db.Repositories.Add(first);
        await db.SaveChangesAsync();

        var collision = RepositoryWith(5004, "owner/name-conflict");
        db.Repositories.Add(collision);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var inner = ex.InnerException as PostgresException;
        Assert.NotNull(inner);
        Assert.Equal("23505", inner.SqlState); // unique_violation
    }

    [Fact]
    public async Task DuplicateGitHubId_IsPreventedByUniqueIndex()
    {
        await using var db = await CreateContextAsync();
        var first = RepositoryWith(5005, "owner/github-dup");
        db.Repositories.Add(first);
        await db.SaveChangesAsync();

        var collision = RepositoryWith(5005, "owner/github-dup-other-name");
        db.Repositories.Add(collision);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var inner = ex.InnerException as PostgresException;
        Assert.NotNull(inner);
        Assert.Equal("23505", inner.SqlState);
    }

    [Fact]
    public async Task Upsert_MultipleRepositories_InsertsOnlyUnknownGitHubIds()
    {
        await using var db = await CreateContextAsync();
        var store = new RepositoryStore(db);
        var existing = RepositoryWith(5006, "owner/already-known", stars: 3);
        await store.UpsertAsync([existing], CancellationToken.None);

        var refresh = RepositoryWith(5006, "owner/already-known", stars: 9);
        var brandNew = RepositoryWith(5007, "owner/fresh");
        await store.UpsertAsync([refresh, brandNew], CancellationToken.None);

        Assert.Equal(2, await db.Repositories.CountAsync());
        Assert.Equal(9, (await db.Repositories.SingleAsync(r => r.GitHubId == 5006)).Stars);
    }
}

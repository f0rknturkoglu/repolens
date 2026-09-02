using Microsoft.EntityFrameworkCore;
using RepoLens.Domain.Discovery;
using RepoLens.Infrastructure.Persistence;
using RepoLens.IntegrationTests.Fixtures;

namespace RepoLens.IntegrationTests.Enrichment;

/// <summary>Shared helpers for enrichment tests running against the container DB.</summary>
public static class EnrichmentTestData
{
    public static async Task<RepoLensDbContext> CreateCleanContextAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RepoLensDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var db = new RepoLensDbContext(options);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM enrichment_jobs;
            DELETE FROM search_documents;
            DELETE FROM repository_snapshots;
            DELETE FROM repository_readmes;
            DELETE FROM repository_languages;
            DELETE FROM repository_topics;
            DELETE FROM repositories;
            """);
        return db;
    }

    public static async Task<Repository> InsertRepositoryAsync(
        RepoLensDbContext db,
        long gitHubId,
        int stars = 0)
    {
        var repository = Repository.Create(
            new Repository.RepositoryData(
                gitHubId,
                "owner-x",
                $"repo-{gitHubId}",
                $"owner-x/repo-{gitHubId}",
                "Test repository",
                $"https://github.com/owner-x/repo-{gitHubId}",
                "main",
                "C#",
                stars,
                0,
                0,
                false,
                false,
                "MIT",
                DateTimeOffset.UtcNow.AddYears(-1),
                DateTimeOffset.UtcNow.AddDays(-1),
                null),
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();
        return repository;
    }
}

[Collection(PostgresContainerFixture.CollectionName)]
public sealed class EnrichmentJobStoreTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task TryEnqueueMany_InsertsPendingJobsOnlyOnce()
    {
        await using var db = await EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        var repo1 = await EnrichmentTestData.InsertRepositoryAsync(db, 91001);
        var repo2 = await EnrichmentTestData.InsertRepositoryAsync(db, 91002);
        var repo3 = await EnrichmentTestData.InsertRepositoryAsync(db, 91003);
        var jobs = new EnrichmentJobStore(db);

        var first = await jobs.TryEnqueueManyAsync([repo1.Id, repo2.Id, repo3.Id], CancellationToken.None);
        var second = await jobs.TryEnqueueManyAsync([repo1.Id, repo2.Id], CancellationToken.None);

        Assert.Equal(3, first);
        Assert.Equal(0, second); // active jobs exist → no duplicates
        Assert.Equal(3, await db.EnrichmentJobs.CountAsync());
    }

    [Fact]
    public async Task ClaimDue_ClaimsOnlyDuePendingJobsAndIncrementsAttempts()
    {
        await using var db = await EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        var repo = await EnrichmentTestData.InsertRepositoryAsync(db, 91004);
        var jobs = new EnrichmentJobStore(db);
        await jobs.TryEnqueueAsync(repo.Id, CancellationToken.None);

        // Move one job's next attempt into the future: it must not be claimed.
        await db.EnrichmentJobs.ExecuteUpdateAsync(
            j => j.SetProperty(e => e.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var claimed = await jobs.ClaimDueAsync(10, CancellationToken.None);
        Assert.Empty(claimed);

        // Make it due again and claim.
        await db.EnrichmentJobs.ExecuteUpdateAsync(
            j => j.SetProperty(e => e.NextAttemptAtUtc, (DateTimeOffset?)null));
        await db.SaveChangesAsync();

        claimed = await jobs.ClaimDueAsync(10, CancellationToken.None);
        var job = Assert.Single(claimed);
        Assert.Equal(Domain.Enrichment.EnrichmentJobStatus.Processing, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.NotNull(job.StartedAtUtc);
    }

    [Fact]
    public async Task RecoverInterrupted_ReturnsStaleProcessingJobsToPending()
    {
        await using var db = await EnrichmentTestData.CreateCleanContextAsync(postgres.ConnectionString);
        var repo = await EnrichmentTestData.InsertRepositoryAsync(db, 91005);
        var jobs = new EnrichmentJobStore(db);
        await jobs.TryEnqueueAsync(repo.Id, CancellationToken.None);
        var claimed = await jobs.ClaimDueAsync(1, CancellationToken.None);
        Assert.Single(claimed);

        // Simulate a crash: the job stays Processing with a stale StartedAt.
        await db.EnrichmentJobs.ExecuteUpdateAsync(
            j => j.SetProperty(e => e.StartedAtUtc, DateTimeOffset.UtcNow.AddHours(-2)));
        await db.SaveChangesAsync();

        var recovered = await jobs.RecoverInterruptedAsync(TimeSpan.FromMinutes(15), CancellationToken.None);
        Assert.Equal(1, recovered);

        var job = await db.EnrichmentJobs.SingleAsync();
        Assert.Equal(Domain.Enrichment.EnrichmentJobStatus.Pending, job.Status);
        Assert.Equal(1, job.Attempts); // attempts survive the crash

        // The recovered job is claimable again (no work lost).
        var reClaimed = await jobs.ClaimDueAsync(1, CancellationToken.None);
        var rerun = Assert.Single(reClaimed);
        Assert.Equal(2, rerun.Attempts);
    }
}

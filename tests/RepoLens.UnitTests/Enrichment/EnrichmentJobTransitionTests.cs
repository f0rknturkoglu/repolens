using RepoLens.Domain.Enrichment;

namespace RepoLens.UnitTests.Enrichment;

public sealed class EnrichmentJobTransitionTests
{
    private static EnrichmentJob NewJob() => EnrichmentJob.ForRepository(42, DateTimeOffset.UtcNow);

    private static EnrichmentJob ProcessingJob()
    {
        var job = NewJob();
        job.Claim(DateTimeOffset.UtcNow);
        return job;
    }

    [Fact]
    public void ForRepository_StartsPendingWithZeroAttempts()
    {
        var job = NewJob();

        Assert.Equal(EnrichmentJobStatus.Pending, job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.Equal(42, job.RepositoryId);
    }

    [Fact]
    public void Claim_MarksProcessingAndIncrementsAttempts()
    {
        var job = NewJob();

        job.Claim(DateTimeOffset.UtcNow);

        Assert.Equal(EnrichmentJobStatus.Processing, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.NotNull(job.StartedAtUtc);
    }

    [Fact]
    public void Complete_ClosesJobAndClearsErrors()
    {
        var job = ProcessingJob();
        job.ScheduleRetry(EnrichmentErrorCategory.Transient, "boom", DateTimeOffset.UtcNow);
        job.Claim(DateTimeOffset.UtcNow); // retry was processed again

        job.Complete(DateTimeOffset.UtcNow);

        Assert.Equal(EnrichmentJobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Null(job.LastError);
        Assert.Equal(EnrichmentErrorCategory.None, job.LastErrorCategory);
    }

    [Fact]
    public void ScheduleRetry_ReturnsToPendingWithNextAttempt()
    {
        var job = ProcessingJob();
        var nextAttempt = DateTimeOffset.UtcNow.AddMinutes(5);

        job.ScheduleRetry(EnrichmentErrorCategory.RateLimit, "rate limited", nextAttempt);

        Assert.Equal(EnrichmentJobStatus.Pending, job.Status);
        Assert.Equal(nextAttempt, job.NextAttemptAtUtc);
        Assert.Equal(EnrichmentErrorCategory.RateLimit, job.LastErrorCategory);
        Assert.Equal("rate limited", job.LastError);
        Assert.Null(job.StartedAtUtc);
    }

    [Fact]
    public void Fail_MarksJobFailedWithCategory()
    {
        var job = ProcessingJob();

        job.Fail(EnrichmentErrorCategory.Permanent, "repository gone", DateTimeOffset.UtcNow);

        Assert.Equal(EnrichmentJobStatus.Failed, job.Status);
        Assert.Equal(EnrichmentErrorCategory.Permanent, job.LastErrorCategory);
        Assert.NotNull(job.CompletedAtUtc);
    }

    [Fact]
    public void Recover_ReturnsInterruptedJobToPending_KeepingAttemptCount()
    {
        var job = ProcessingJob(); // attempts == 1

        job.Recover();

        Assert.Equal(EnrichmentJobStatus.Pending, job.Status);
        Assert.Null(job.StartedAtUtc);
        Assert.Equal(1, job.Attempts); // a retry after recovery is a new attempt
    }

    [Fact]
    public void ClaimOnProcessingJob_Throws()
    {
        var job = ProcessingJob();

        Assert.Throws<InvalidOperationException>(() => job.Claim(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompleteOnPendingJob_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewJob().Complete(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FailOnCompletedJob_Throws()
    {
        var job = ProcessingJob();
        job.Complete(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            job.Fail(EnrichmentErrorCategory.Transient, "x", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ScheduleRetryOnCompletedJob_Throws()
    {
        var job = ProcessingJob();
        job.Complete(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            job.ScheduleRetry(EnrichmentErrorCategory.Transient, "x", null));
    }

    [Fact]
    public void LongErrorMessages_AreTruncated()
    {
        var job = ProcessingJob();

        job.Fail(EnrichmentErrorCategory.Permanent, new string('x', 2000), DateTimeOffset.UtcNow);

        Assert.Equal(500, job.LastError!.Length);
    }
}

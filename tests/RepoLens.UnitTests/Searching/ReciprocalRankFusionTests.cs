using RepoLens.Application.Searching;

namespace RepoLens.UnitTests.Searching;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Merge_CombinesRanksFromBothLegs()
    {
        var merged = ReciprocalRankFusion.Merge([10, 20, 30], [30, 20, 40]);

        // 30 appears in both lists (duplicate suppressed, contribution doubled);
        // 10 only in keyword; 40 only in vector.
        Assert.Equal([30, 20, 10, 40], merged.Select(r => r.RepositoryId).ToArray());
        Assert.True(merged[0].Score >= merged[1].Score);
        Assert.True(merged[1].Score >= merged[2].Score);
    }

    [Fact]
    public void Merge_SingleLegKeepsItsOrder()
    {
        var merged = ReciprocalRankFusion.Merge([7, 5, 3], []);

        Assert.Equal([7, 5, 3], merged.Select(r => r.RepositoryId).ToArray());
        Assert.Equal(1.0, merged[0].Score, precision: 6); // best hit normalized to 1
        Assert.True(merged[0].Score > merged[1].Score);
    }

    [Fact]
    public void Merge_EmptyInputs_ReturnsEmpty()
    {
        Assert.Empty(ReciprocalRankFusion.Merge([], []));
    }

    [Fact]
    public void Merge_IsDeterministicAcrossTies()
    {
        // Same score for 1 and 2 (both rank ~equal in both legs) → id order decides.
        var first = ReciprocalRankFusion.Merge([1, 2, 3], [2, 1, 3]);
        var second = ReciprocalRankFusion.Merge([1, 2, 3], [2, 1, 3]);

        Assert.Equal(first.Select(r => r.RepositoryId), second.Select(r => r.RepositoryId));
        Assert.True(first.Select(r => r.RepositoryId).SequenceEqual(first.Select(r => r.RepositoryId).OrderBy(x => x))
            || first[0].Score > first[^1].Score); // sorted by score desc, ids asc on ties
    }

    [Fact]
    public void Merge_DuplicateHitInBothLegsYieldsSingleEntry()
    {
        var merged = ReciprocalRankFusion.Merge([5, 6], [5, 7]);

        Assert.Single(merged, r => r.RepositoryId == 5);
        Assert.Equal(3, merged.Count);
    }

    [Fact]
    public void Merge_TracksBestRankPerRepository()
    {
        var merged = ReciprocalRankFusion.Merge([9, 8], [8, 9, 7]);

        var nine = Assert.Single(merged, r => r.RepositoryId == 9);
        Assert.Equal(1, nine.BestRank);
    }

    [Fact]
    public void Merge_VectorOnly_KeepsItsOrderAndNormalizes()
    {
        var merged = ReciprocalRankFusion.Merge([], [101, 102, 103]);

        Assert.Equal([101, 102, 103], merged.Select(r => r.RepositoryId).ToArray());
        Assert.Equal(1.0, merged[0].Score, precision: 6);
        Assert.True(merged[0].Score > merged[1].Score);
        Assert.True(merged[1].Score > merged[2].Score);
    }

    [Fact]
    public void Merge_IdenticalLists_PreservesOrderAndRanks()
    {
        var merged = ReciprocalRankFusion.Merge([42, 84], [42, 84]);

        Assert.Equal([42, 84], merged.Select(r => r.RepositoryId).ToArray());
        Assert.Equal(1.0, merged[0].Score, precision: 6);
        Assert.Equal(1, merged[0].BestRank);
        Assert.Equal(2, merged[1].BestRank);
    }

    [Fact]
    public void Merge_ExactTies_ResolvesDeterministicallyByIdAsc()
    {
        // 50 is rank 1 in keyword, rank 2 in vector -> score: k/(k+1) + k/(k+2)
        // 20 is rank 2 in keyword, rank 1 in vector -> score: k/(k+2) + k/(k+1) (identical sum!)
        var merged = ReciprocalRankFusion.Merge([50, 20], [20, 50]);

        Assert.Equal(2, merged.Count);
        Assert.Equal(merged[0].Score, merged[1].Score);
        // Tie-breaker is repository ID ascending: 20 must come before 50
        Assert.Equal(20, merged[0].RepositoryId);
        Assert.Equal(50, merged[1].RepositoryId);
    }
}

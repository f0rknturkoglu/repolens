using RepoLens.Application.Recommendation;

namespace RepoLens.UnitTests.Recommendation;

public sealed class RecommendationUnitTests
{
    private static RecommendationItem Item(string title, string category, double score) => new()
    {
        Title = title,
        Category = category,
        Summary = "summary",
        Problem = "problem",
        TargetUser = "target",
        Score = score,
        WhyRankedHere = ["+ test"],
        Evidence = new RecommendationItem.EvidenceBlock
        {
            Originality = 0.5,
            PortfolioMarginalValue = 0.5,
            Feasibility = new RecommendationItem.EvidenceBlock.FeasibilityJson
            {
                Scope = "Medium",
                Score = 0.7,
                Reasons = ["test"],
            },
            GoalAlignment = 0.5,
            InterestAlignment = 0.5,
            Landscape = new RecommendationItem.EvidenceBlock.CandidateLandscapeJson
            {
                CandidateCount = 5,
                NoveltyScore = 50,
                Density = 0.2,
                LargestClusterShare = 0.5,
                ActiveCount = 2,
                Competitors = [],
            },
        },
        DifferentiationOpportunity = "x",
        MvpSuggestion = "y",
    };

    [Fact]
    public void Scorer_IsDeterministicAndWeighted()
    {
        var first = RecommendationScorer.Score(0.8, 0.6, 0.7, 0.5, 0.4);
        var second = RecommendationScorer.Score(0.8, 0.6, 0.7, 0.5, 0.4);

        Assert.Equal(first, second);
        // Originality dominates: a novel idea scores above a crowded one otherwise equal.
        var crowded = RecommendationScorer.Score(0.1, 0.6, 0.7, 0.5, 0.4);
        Assert.True(first > crowded);
    }

    [Fact]
    public void Feasibility_MoreInfrastructureMeansBiggerScope()
    {
        var small = CandidateFeasibility.Assess("a cli tool", "2");
        var large = CandidateFeasibility.Assess("distributed queue with kafka, postgres, kubernetes and llm model serving", "6");

        Assert.Equal("Small", small.Scope);
        Assert.True(large.Scope is "Large" or "Very Large");
        Assert.True(large.Score < small.Score);
        Assert.Contains(large.Reasons, r => r.Contains("infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Feasibility_TightWindowPenalizesLargeScope()
    {
        var tight = CandidateFeasibility.Assess("kubernetes platform with kafka streaming and postgres", "2");
        var loose = CandidateFeasibility.Assess("kubernetes platform with kafka streaming and postgres", "12");

        Assert.True(tight.Score < loose.Score);
    }

    [Fact]
    public void Diversity_TopThreeHaveDistinctCategories()
    {
        var items = new List<RecommendationItem>
        {
            Item("a", "Databases", 0.9),
            Item("b", "Databases", 0.85), // same category as the top — must be skipped
            Item("c", "DevOps", 0.7),
            Item("d", "AI/ML", 0.5),
        };

        var chosen = RecommendationService.SelectDiverse(items);

        Assert.Equal(3, chosen.Count);
        Assert.Equal(["a", "c", "d"], chosen.Select(i => i.Title).ToArray());
        Assert.Equal(3, chosen.Select(i => i.Category).Distinct().Count());
    }

    [Fact]
    public void Diversity_RespectsScoreOrder()
    {
        var items = new List<RecommendationItem>
        {
            Item("top", "Databases", 0.95),
            Item("second", "DevOps", 0.6),
            Item("third", "Testing", 0.5),
        };

        var chosen = RecommendationService.SelectDiverse(items);

        Assert.Equal("top", chosen[0].Title);
    }

    [Fact]
    public void Alignment_CategoryKeywordsInTextRaiseScore()
    {
        var high = RecommendationScorer.Alignment("I want to build database migration tooling with postgres", "Databases");
        var low = RecommendationScorer.Alignment("I build weather visualizations", "Databases");

        Assert.True(high > low);
    }

    [Fact]
    public void RequestHash_IgnoresInterestOrder()
    {
        var a = RecommendationService.RequestHash(new RecommendationInput("build tools", ["databases", "testing"], null, null));
        var b = RecommendationService.RequestHash(new RecommendationInput("build tools", ["testing", "databases"], null, null));

        Assert.Equal(a, b);
    }
}

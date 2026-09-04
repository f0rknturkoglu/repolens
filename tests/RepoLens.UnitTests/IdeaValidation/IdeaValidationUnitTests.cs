using RepoLens.Application.Ai;
using RepoLens.Application.Analysis;
using RepoLens.Application.IdeaValidation;

namespace RepoLens.UnitTests.IdeaValidation;

public sealed class IdeaValidationUnitTests
{
    private sealed class FakeLlm(bool configured, string json) : ILlmClient
    {
        public string Model => "test-model";
        public bool IsConfigured => configured;
        public int Calls { get; private set; }

        public Task<LlmJsonResponse> CompleteJsonAsync(LlmJsonRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return configured
                ? Task.FromResult(new LlmJsonResponse(json, Model))
                : Task.FromException<LlmJsonResponse>(new LlmUnavailableException(null));
        }
    }

    private static RepoFeatures Repo(
        long id,
        string name,
        int stars = 100,
        DateTimeOffset? pushed = null,
        params string[] topics) => new()
    {
        Id = id,
        GitHubId = id + 200_000,
        Name = name,
        FullName = $"owner/{name}",
        Description = null,
        PrimaryLanguage = "Go",
        Stars = stars,
        Forks = 1,
        OpenIssues = 0,
        IsArchived = false,
        IsFork = false,
        PushedAtUtc = pushed ?? DateTimeOffset.UtcNow,
        CreatedAtUtc = DateTimeOffset.UtcNow.AddYears(-2),
        Topics = topics,
    };

    [Fact]
    public void FallbackPlan_IsDeterministicAndBounded()
    {
        var first = SearchPlanBuilder.FallbackPlan("AI expense tracker with receipt scanning");
        var second = SearchPlanBuilder.FallbackPlan("AI expense tracker with receipt scanning");

        Assert.Equal(first.Select(q => q.Text), second.Select(q => q.Text));
        Assert.All(first, q => Assert.Equal("fallback", q.Source));
        Assert.InRange(first.Count, 1, SearchPlanBuilder.MaxQueries);
    }

    [Fact]
    public void FallbackPlan_ContainsIdeaAndDedupedTerms()
    {
        var plan = SearchPlanBuilder.FallbackPlan("postgres migration load testing");

        var texts = plan.Select(q => q.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("postgres migration load testing", texts);
        Assert.Contains(texts, t => t.Contains("postgres", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildAsync_WithoutLlm_UsesFallbackOnly()
    {
        var plan = await SearchPlanBuilder.BuildAsync("postgres migration", null, CancellationToken.None);

        Assert.All(plan.Queries, q => Assert.Equal("fallback", q.Source));
    }

    [Fact]
    public async Task BuildAsync_WithLlm_ValidatesAndMergesQueries()
    {
        var llm = new FakeLlm(true, """{"queries":["migration replay tool", "sql migration simulator"]}""");

        var plan = await SearchPlanBuilder.BuildAsync("postgres migration", llm, CancellationToken.None);

        Assert.Contains(plan.Queries, q => q.Source == "llm" && q.Text == "migration replay tool");
        Assert.Contains(plan.Queries, q => q.Source == "llm" && q.Text == "sql migration simulator");
        Assert.Contains(plan.Queries, q => q.Source == "fallback"); // safety net
    }

    [Fact]
    public async Task BuildAsync_MalformedLlmOutput_FallsBack()
    {
        var llm = new FakeLlm(true, """{"queries": [123, null]}""");

        var plan = await SearchPlanBuilder.BuildAsync("postgres migration", llm, CancellationToken.None);

        Assert.All(plan.Queries, q => Assert.Equal("fallback", q.Source));
    }

    [Fact]
    public async Task BuildAsync_LlmUnavailable_FallsBack()
    {
        var llm = new FakeLlm(false, "unused");

        var plan = await SearchPlanBuilder.BuildAsync("postgres migration", llm, CancellationToken.None);

        Assert.All(plan.Queries, q => Assert.Equal("fallback", q.Source));
    }

    [Fact]
    public async Task BuildAsync_RejectsQualifierQueriesFromLlm()
    {
        var llm = new FakeLlm(true, """{"queries":["language:go migration"]}""");

        var plan = await SearchPlanBuilder.BuildAsync("postgres migration", llm, CancellationToken.None);

        Assert.DoesNotContain(plan.Queries, q => q.Text == "language:go migration");
    }

    [Fact]
    public void Novelty_NoCandidates_ScoresFullNoveltyWithCompetitorGap()
    {
        var result = IdeaNoveltyScorer.Compute([], ["migration"], DateTimeOffset.UtcNow);

        Assert.Equal(100, result.Score);
        Assert.Empty(result.Competitors);
        Assert.Contains(result.Components, c => c.Key == "candidates");
    }

    [Fact]
    public void Novelty_ManyNearIdenticalCompetitors_ScoresLow()
    {
        var idea = "postgres migration tool";
        var clones = Enumerable.Range(1, 4)
            .Select(i => Repo(i, "postgres-migration-tool-{i}", topics: ["postgres", "migration"]))
            .ToList();
        var unrelated = Repo(99, "weather-app", topics: ["react", "charts"]);

        var result = IdeaNoveltyScorer.Compute([.. clones, unrelated], SearchPlanBuilder.IdeaTerms(idea), DateTimeOffset.UtcNow);

        Assert.True(result.Score < 45, $"expected low novelty, got {result.Score}");
        Assert.True(result.Competitors[0].Overlap > 0.3);
        Assert.Contains(result.Competitors[0].Reasons, r => r.Contains("migration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Novelty_SaturatedLandscape_ScoresLowerThanSparseLandscape()
    {
        var idea = SearchPlanBuilder.IdeaTerms("sql migration benchmark");
        var now = DateTimeOffset.UtcNow;

        // Sparse: 2 unrelated candidates.
        var sparse = new List<RepoFeatures>
        {
            Repo(1, "alpha", topics: ["aaa"]),
            Repo(2, "beta", topics: ["bbb"]),
        };

        // Saturated: 5 near-identical active candidates that all share idea topics.
        var saturated = Enumerable.Range(1, 5)
            .Select(i => Repo(i, $"sql-migration-benchmark-{i}", pushed: now.AddDays(-1), topics: ["sql", "migration", "benchmark"]))
            .ToList();

        var sparseResult = IdeaNoveltyScorer.Compute(sparse, idea, now);
        var saturatedResult = IdeaNoveltyScorer.Compute(saturated, idea, now);

        Assert.True(sparseResult.Score > saturatedResult.Score,
            $"sparse ({sparseResult.Score}) should score more novel than saturated ({saturatedResult.Score})");
    }

    [Fact]
    public void Novelty_FormulaIsVersionedAndDeterministic()
    {
        var idea = SearchPlanBuilder.IdeaTerms("database migration tool");
        var candidates = new List<RepoFeatures>
        {
            Repo(1, "db-migrate", topics: ["migration", "database"]),
            Repo(2, "sql-migrate", topics: ["migration", "database"]),
        };

        var first = IdeaNoveltyScorer.Compute(candidates, idea, DateTimeOffset.UtcNow);
        var second = IdeaNoveltyScorer.Compute(candidates, idea, DateTimeOffset.UtcNow);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal("novelty-v1", first.FormulaVersion);
    }

    [Fact]
    public void Gaps_CommonTermsProduceSaturatedHypothesis()
    {
        var candidates = Enumerable.Range(1, 6)
            .Select(i => Repo(i, $"migrator-{i}", topics: ["migration", "sql"]))
            .ToList();

        var gaps = GapHypothesisBuilder.Build(candidates, ["migration", "sql", "quantum"]);

        Assert.Contains(gaps, g => g.Kind == "saturated" && g.Statement.Contains("migration", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(gaps, g => g.Kind == "gap" && g.Statement.Contains("quantum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Gaps_StatementsAreScopedToAnalyzedSet()
    {
        var gaps = GapHypothesisBuilder.Build(
            [Repo(1, "only", topics: ["migration"])],
            ["migration", "etl"]);

        Assert.All(gaps, g =>
            Assert.Contains("candidate", g.Statement, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IdeaValidation_HashStableAcrossWhitespace()
    {
        var first = IdeaValidationService.Hash(IdeaValidationService.Normalize("Postgres Migration  Tool"));
        var second = IdeaValidationService.Hash(IdeaValidationService.Normalize("postgres migration tool"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Novelty_ComponentWeights_SumTo100Percent()
    {
        var candidates = new List<RepoFeatures>
        {
            Repo(1, "db-migrate", topics: ["migration", "database"]),
            Repo(2, "sql-migrate", topics: ["migration", "database"]),
        };
        var idea = SearchPlanBuilder.IdeaTerms("database migration tool");

        var result = IdeaNoveltyScorer.Compute(candidates, idea, DateTimeOffset.UtcNow);

        Assert.Equal(5, result.Components.Count);
        Assert.Equal(100, result.Components.Sum(c => c.WeightPercent));
        Assert.Equal(IdeaNoveltyScorer.WeightClosestCompetitorPercent, result.Components.First(c => c.Key == "competitor_gap").WeightPercent);
        Assert.Equal(IdeaNoveltyScorer.WeightDensityPercent, result.Components.First(c => c.Key == "density").WeightPercent);
        Assert.Equal(IdeaNoveltyScorer.WeightClusterDominancePercent, result.Components.First(c => c.Key == "cluster_dominance").WeightPercent);
        Assert.Equal(IdeaNoveltyScorer.WeightActiveCompetitionPercent, result.Components.First(c => c.Key == "active_competition").WeightPercent);
        Assert.Equal(IdeaNoveltyScorer.WeightConceptRedundancyPercent, result.Components.First(c => c.Key == "concept_redundancy").WeightPercent);
    }

    [Fact]
    public void Novelty_ZeroCandidates_Has100PercentWeightOnCandidateComponent()
    {
        var result = IdeaNoveltyScorer.Compute([], ["tool"], DateTimeOffset.UtcNow);

        Assert.Single(result.Components);
        Assert.Equal("candidates", result.Components[0].Key);
        Assert.Equal(100, result.Components[0].WeightPercent);
    }
}

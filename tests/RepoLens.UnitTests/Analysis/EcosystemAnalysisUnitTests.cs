using RepoLens.Application.Analysis;

namespace RepoLens.UnitTests.Analysis;

public sealed class EcosystemAnalysisUnitTests
{
    private static RepoFeatures Repo(
        long id,
        string name,
        string? language = "Go",
        int stars = 100,
        bool archived = false,
        bool isFork = false,
        params string[] topics) => new()
    {
        Id = id,
        GitHubId = id + 100_000,
        Name = name,
        FullName = $"owner/{name}",
        Description = "desc",
        PrimaryLanguage = language,
        Stars = stars,
        Forks = 1,
        OpenIssues = 0,
        IsArchived = archived,
        IsFork = isFork,
        PushedAtUtc = DateTimeOffset.UtcNow,
        CreatedAtUtc = DateTimeOffset.UtcNow.AddYears(-2),
        Topics = topics,
    };

    [Fact]
    public void Variants_AreDeterministicAndBounded()
    {
        var first = EcosystemAnalysisService.BuildVariants("postgres migration safety");
        var second = EcosystemAnalysisService.BuildVariants("postgres migration safety");

        Assert.Equal(first.Select(v => v.Query), second.Select(v => v.Query));
        Assert.InRange(first.Count, 1, 3);
        Assert.Contains(first, v => v.Label == "exact");
    }

    [Fact]
    public void Variants_StripQualifiersForTopicVariant()
    {
        var variants = EcosystemAnalysisService.BuildVariants("language:go migration");

        Assert.Contains(variants, v => v.Label == "topic" && v.Query.StartsWith("topic:", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateQuery_RejectsBlankAndOverlong()
    {
        Assert.NotNull(EcosystemAnalysisService.ValidateQuery("  "));
        Assert.NotNull(EcosystemAnalysisService.ValidateQuery(new string('x', 201)));
        Assert.Null(EcosystemAnalysisService.ValidateQuery("postgres migration"));
    }

    [Fact]
    public void Similarity_SharedTopicsScoreAboveThreshold_SameLanguageToo()
    {
        var a = Repo(1, "migra", "Go", topics: ["migration", "postgres", "sql"]);
        var b = Repo(2, "pgroll", "Go", topics: ["migration", "postgres"]);

        var score = RepoSimilarity.Score(a, b);

        Assert.True(score >= RepoSimilarity.EdgeThreshold);
    }

    [Fact]
    public void Similarity_UnrelatedRepos_ScoreBelowThreshold()
    {
        var a = Repo(1, "migra", "Go", topics: ["migration", "postgres"]);
        var b = Repo(2, "weather-charts", "JavaScript", topics: ["charts", "react"]);

        Assert.True(RepoSimilarity.Score(a, b) < RepoSimilarity.EdgeThreshold);
    }

    [Fact]
    public void Similarity_NoSharedSignals_ReturnsZero()
    {
        var a = Repo(1, "alpha", "Go", topics: ["aaa"]);
        var b = Repo(2, "beta", "Rust", topics: ["bbb"]);

        Assert.Equal(0.0, RepoSimilarity.Score(a, b), precision: 10);
    }

    [Fact]
    public void Edges_OnlyConnectsAboveThreshold()
    {
        var repos = new List<RepoFeatures>
        {
            Repo(1, "migra", "Go", topics: ["migration", "postgres"]),
            Repo(2, "pgroll", "Go", topics: ["migration", "postgres"]),
            Repo(3, "charts", "JS", topics: ["react"]),
        };

        var edges = RepoSimilarity.Edges(repos);

        var pair = Assert.Single(edges);
        Assert.Contains(pair.A.Id, new HashSet<long> { 1, 2 });
        Assert.Contains(pair.B.Id, new HashSet<long> { 1, 2 });
    }

    [Fact]
    public void ConnectedComponents_DeterministicAndTransitive()
    {
        // 1-2-3 chain plus an isolated repo.
        var repos = new List<RepoFeatures>
        {
            Repo(1, "migra", "Go", topics: ["migration", "postgres"]),
            Repo(2, "pgroll", "Go", topics: ["migration", "postgres"]),
            Repo(3, "flyway", "Go", topics: ["migration"]),
            Repo(4, "solo-chart", "JS", topics: ["react"]),
        };
        var edges = RepoSimilarity.Edges(repos);

        var components = RepoSimilarity.ConnectedComponents(repos, edges);

        var repeated = RepoSimilarity.ConnectedComponents(repos, edges);
        Assert.Equal(components.Count, repeated.Count);
        Assert.Equal(components.Select(c => c[0].Id), repeated.Select(c => c[0].Id));

        var migrationCluster = Assert.Single(components, c => c[0].Id == 1);
        Assert.Equal(3, migrationCluster.Count); // transitive: 1-2 and 2-3 chain
        Assert.Single(components, c => c[0].Id == 4);
    }

    [Fact]
    public void Metrics_EmptyLandscape_ProducesZerosAndNoEvidenceErrors()
    {
        var metrics = EcosystemMetricsCalculator.Compute([], []);

        Assert.Equal(0, metrics.CandidateCount);
        Assert.Equal(0.0, metrics.LargestClusterShare);
        Assert.Equal(0.0, metrics.StarsMedian);
        Assert.Equal(0.0, metrics.StarsConcentration);
        Assert.DoesNotContain(metrics.AgeDistribution, b => b.Count > 0);
    }

    [Fact]
    public void Metrics_OneDominantProject_ConcentrationAndShareReflectIt()
    {
        var repos = new List<RepoFeatures>
        {
            Repo(1, "big", "Go", stars: 900, topics: ["migration"]),
            Repo(2, "small1", "Go", stars: 50, topics: ["migration", "postgres"]),
            Repo(3, "small2", "Go", stars: 50, topics: ["migration"]),
            Repo(4, "tiny", "JS", stars: 1, topics: ["other"]),
        };
        var edges = RepoSimilarity.Edges(repos);

        var metrics = EcosystemMetricsCalculator.Compute(repos, edges);

        Assert.Equal(4, metrics.CandidateCount);
        Assert.True(metrics.StarsConcentration >= 0.85, "top-5 concentration should be near 1 with one dominant project");
        Assert.Equal(3, metrics.PrimaryLanguages.Single(l => l.Value == "Go").Count);
        Assert.Equal(0.0, metrics.ArchivedRatio, precision: 6); // none archived
        Assert.True(metrics.SimilarityDensity > 0, "close migration repos should produce density");
        Assert.Contains(metrics.Evidence, e => e.Contains("Top", StringComparison.Ordinal));
    }

    [Fact]
    public void Metrics_NearDuplicateProjects_ProduceHighDensityAndLargeCluster()
    {
        var repos = Enumerable.Range(1, 5)
            .Select(i => Repo(i, $"migration-tool-{i}", "Go", stars: 10, topics: ["migration", "sql", "postgres"]))
            .ToList();

        var edges = RepoSimilarity.Edges(repos);
        var metrics = EcosystemMetricsCalculator.Compute(repos, edges);

        Assert.True(metrics.SimilarityDensity > 0.6, "nearly identical repos should yield high pairwise similarity");
        Assert.Equal(1, metrics.ClusterCount); // one giant cluster
        Assert.Equal(1.0, metrics.LargestClusterShare, precision: 6);
    }

    [Fact]
    public void Metrics_MostlyArchivedEcosystem_ArchivedRatioHigh()
    {
        var repos = Enumerable.Range(1, 8)
            .Select(i => Repo(i, $"old-{i}", "Go", archived: i > 2, topics: ["legacy"]))
            .ToList();

        var metrics = EcosystemMetricsCalculator.Compute(repos, []);

        Assert.True(metrics.ArchivedRatio >= 0.6, "6 of 8 archived should be ≥ 0.6");
        Assert.Contains(metrics.Evidence, e => e.Contains("archived", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Metrics_RecentActivityOnlyCountsRecentPushes()
    {
        var repos = new List<RepoFeatures>
        {
            new RepoFeatures
            {
                Id = 1, GitHubId = 100001, Name = "old", FullName = "owner/old",
                Description = "desc", PrimaryLanguage = "Go", Stars = 100, Forks = 1,
                OpenIssues = 0, IsArchived = false, IsFork = false,
                PushedAtUtc = DateTimeOffset.UtcNow.AddDays(-400),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddYears(-2), Topics = [],
            },
            Repo(2, "fresh", "Go"),
        };

        var metrics = EcosystemMetricsCalculator.Compute(repos, []);

        Assert.Equal(0.5, metrics.RecentlyActiveRatio, precision: 6);
    }

    [Fact]
    public void Clusterizer_IsCompletelyDeterministicAcrossPermutations()
    {
        var reposA = new List<RepoFeatures>
        {
            Repo(10, "postgres-migrator", "Go", topics: ["migration", "postgres", "sql"]),
            Repo(20, "sql-migrate", "Go", topics: ["migration", "sql"]),
            Repo(30, "db-bench", "Rust", topics: ["benchmark", "database"]),
            Repo(40, "db-perf", "Rust", topics: ["benchmark", "database"]),
            Repo(50, "unrelated-ui", "TypeScript", topics: ["react", "ui"]),
        };

        // Permuted list in reverse
        var reposB = reposA.AsEnumerable().Reverse().ToList();

        var edgesA = RepoSimilarity.Edges(reposA);
        var edgesB = RepoSimilarity.Edges(reposB);

        var clustersA = Clusterizer.Build(reposA, edgesA);
        var clustersB = Clusterizer.Build(reposB, edgesB);

        Assert.Equal(clustersA.Count, clustersB.Count);
        for (var i = 0; i < clustersA.Count; i++)
        {
            Assert.Equal(clustersA[i].Label, clustersB[i].Label);
            Assert.Equal(clustersA[i].Members.Count, clustersB[i].Members.Count);
            for (var m = 0; m < clustersA[i].Members.Count; m++)
            {
                Assert.Equal(clustersA[i].Members[m].RepositoryId, clustersB[i].Members[m].RepositoryId);
                Assert.Equal(clustersA[i].Members[m].Centrality, clustersB[i].Members[m].Centrality);
            }
        }
    }
}

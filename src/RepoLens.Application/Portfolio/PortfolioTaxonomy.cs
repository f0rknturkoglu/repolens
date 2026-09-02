namespace RepoLens.Application.Portfolio;

/// <summary>
/// Versioned portfolio signal taxonomy. Categories map to language names and
/// keyword concepts found in repository descriptions/topics/names. This is the
/// single source for signal classification (deterministic; versioned so future
/// taxonomy changes never rewrite old reports).
/// </summary>
public static class PortfolioTaxonomy
{
    public const string Version = "taxonomy-v1";

    public sealed record Category(string Name, IReadOnlySet<string> Languages, IReadOnlySet<string> Keywords)
    {
        public bool MatchesLanguage(string? language) =>
            language is not null && Languages.Contains(language.ToLowerInvariant());
    }

    public static IReadOnlyList<Category> All { get; } =
    [
        new Category("Backend", Lang("go", "csharp", "java", "python", "rust", "kotlin", "php", "ruby", "scala", "c++", "c", "typescript", "javascript"),
            Word("api", "backend", "server", "service", "rest", "grpc", "graphql", "http")),
        new Category("Frontend", Lang("typescript", "javascript", "react", "vue", "svelte", "angular", "css", "html"),
            Word("frontend", "front-end", "ui", "web", "react", "component", "design-system")),
        new Category("Databases", Lang("sql", "plpgsql"),
            Word("database", "postgres", "postgresql", "mysql", "sqlite", "redis", "sql", "migration", "schema", "orm", "query")),
        new Category("Data Engineering", Lang("python", "scala", "sql"),
            Word("data", "pipeline", "etl", "elt", "spark", "warehouse", "airflow", "analytics", "parquet")),
        new Category("AI/ML", Lang("python"),
            Word("machine-learning", "deep-learning", "llm", "embedding", "rag", "neural", "model", "tensorflow", "pytorch", "nlp", "ai", "inference", "agent")),
        new Category("Testing", Lang("python", "typescript", "javascript"),
            Word("test", "testing", "qa", "e2e", "unit-test", "playwright", "cypress", "coverage", "mock")),
        new Category("DevOps", Lang("hcl", "dockerfile", "shell", "yaml"),
            Word("docker", "kubernetes", "k8s", "terraform", "ci", "cd", "pipeline", "deploy", "helm", "observability", "grafana", "prometheus", "monitoring")),
        new Category("Security", Lang("c", "cpp", "rust", "go", "python"),
            Word("security", "auth", "oauth", "jwt", "crypto", "encryption", "vulnerability", "scan", "sast")),
        new Category("Distributed Systems", Lang("go", "rust", "java", "erlang", "elixir"),
            Word("distributed", "raft", "consensus", "queue", "streaming", "kafka", "cluster", "concurrency", "microservice")),
        new Category("Developer Tooling", Lang("go", "rust", "typescript", "python"),
            Word("cli", "command-line", "linter", "compiler", "formatter", "ide", "plugin", "sdk", "tool", "codegen", "debugger")),
        new Category("Observability", Lang("go", "rust", "python"),
            Word("telemetry", "tracing", "metrics", "logging", "opentelemetry", "jaeger", "prometheus", "sentry", "apm")),
    ];

    private static IReadOnlySet<string> Lang(params string[] values) => values.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> Word(params string[] values) => values.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IdeaMatchesCategory(string ideaText, string categoryName)
    {
        var idea = ideaText.ToLowerInvariant();
        var category = All.FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            return false;
        }

        return category.Keywords.Any(idea.Contains) || category.Languages.Any(idea.Contains);
    }
}

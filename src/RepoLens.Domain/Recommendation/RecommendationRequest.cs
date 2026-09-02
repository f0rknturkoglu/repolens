namespace RepoLens.Domain.Recommendation;

public enum RecommendationStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>
/// A personalized recommendation request and its snapshot: input (goal,
/// interests, optional GitHub profile, constraints) and the ranked candidate
/// results with explainability lines. Everything is persisted so history views
/// (and the cache) can replay the report without new GitHub/LLM calls.
/// </summary>
public sealed class RecommendationRequest
{
    public const string CurrentVersion = "1";
    public const string ScoreFormulaVersion = "rec-v1";

    private RecommendationRequest()
    {
        // EF Core materialization.
        Goal = string.Empty;
        RequestHash = string.Empty;
    }

    private RecommendationRequest(
        string goal,
        string requestHash,
        string? username,
        string interestsJson,
        string constraintsJson,
        DateTimeOffset createdAtUtc)
    {
        Goal = goal;
        RequestHash = requestHash;
        Username = username;
        InterestsJson = interestsJson;
        ConstraintsJson = constraintsJson;
        Version = CurrentVersion;
        Status = RecommendationStatus.Running;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }
    public string Goal { get; private set; }
    public string RequestHash { get; private set; }
    public string? Username { get; private set; }
    public string InterestsJson { get; private set; }
    public string ConstraintsJson { get; private set; }
    public string Version { get; private set; }
    public RecommendationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Serialized ranked recommendations with evidence.</summary>
    public string? ResultJson { get; private set; }

    public static RecommendationRequest Start(
        string goal,
        string requestHash,
        string? username,
        string interestsJson,
        string constraintsJson,
        DateTimeOffset createdAtUtc) =>
        new(goal, requestHash, username, interestsJson, constraintsJson, createdAtUtc);

    public void Complete(string resultJson, DateTimeOffset completedAtUtc)
    {
        Status = RecommendationStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        ResultJson = resultJson;
    }

    public void Fail(DateTimeOffset completedAtUtc)
    {
        Status = RecommendationStatus.Failed;
        CompletedAtUtc = completedAtUtc;
    }
}

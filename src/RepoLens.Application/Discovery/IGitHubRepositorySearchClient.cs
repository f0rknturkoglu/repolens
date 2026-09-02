namespace RepoLens.Application.Discovery;

public sealed record RepositorySearchRequest(string Query, int Page, int PageSize);

/// <summary>
/// Port for GitHub repository search, implemented by the Infrastructure HTTP
/// adapter. Implementations translate GitHub errors into the typed exceptions in
/// <see cref="GitHubSearchExceptions"/> and never leak raw GitHub response text.
/// </summary>
public interface IGitHubRepositorySearchClient
{
    Task<GitHubRepositorySearchResult> SearchAsync(
        RepositorySearchRequest request,
        CancellationToken cancellationToken);
}

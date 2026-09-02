using RepoLens.Api.Identity;
using RepoLens.Application.Identity;
using RepoLens.Domain.Identity;

namespace RepoLens.Api.Endpoints;

/// <summary>Auth + personal history endpoints (same-origin cookie sessions).</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapGet("/status", async (
            GitHubOAuthSettings oauth,
            AuthSessionService sessions,
            IUserStore users,
            CancellationToken cancellationToken) =>
        {
            var current = await sessions.GetCurrentUserAsync(cancellationToken);
            return Results.Ok(new
            {
                Enabled = oauth.IsEnabled,
                User = current is null
                    ? null
                    : new { current.UserId, current.Login, current.Name, current.AvatarUrl },
            });
        });

        group.MapGet("/login", (GitHubOAuthSettings oauth, HttpContext context) =>
        {
            if (!oauth.IsEnabled)
            {
                return Results.NotFound(new { Detail = "GitHub sign-in is not configured on this installation." });
            }

            var scope = Uri.EscapeDataString("read:user");
            var clientId = Uri.EscapeDataString(oauth.ClientId);
            var state = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append("oauth.state", state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
            var url = $"https://github.com/login/oauth/authorize?client_id={clientId}&scope={scope}&state={state}&redirect_uri={Uri.EscapeDataString(oauth.CallbackUrl)}";
            return Results.Redirect(url);
        });

        group.MapGet("/callback", async (
            string? code,
            string? state,
            IGitHubOAuthClient oauth,
            GitHubOAuthSettings settings,
            AuthSessionService sessions,
            IUserStore users,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!settings.IsEnabled || string.IsNullOrWhiteSpace(code))
            {
                return Results.BadRequest(new { Detail = "Sign-in failed." });
            }

            if (string.IsNullOrWhiteSpace(state)
                || !context.Request.Cookies.TryGetValue("oauth.state", out var expectedState)
                || !string.Equals(expectedState, state, StringComparison.Ordinal))
            {
                return Results.BadRequest(new { Detail = "Sign-in state mismatch." });
            }

            var gitHubUser = await oauth.ExchangeCodeAsync(code, cancellationToken);
            var user = await users.UpsertAsync(
                User.Create(
                    gitHubUser.GitHubId,
                    gitHubUser.Login,
                    gitHubUser.Name,
                    gitHubUser.AvatarUrl,
                    DateTimeOffset.UtcNow),
                cancellationToken);
            sessions.SignIn(user.Id, user.GitHubId, user.Login);
            context.Response.Cookies.Delete("oauth.state");
            return Results.Redirect("/");
        });

        group.MapPost("/logout", (AuthSessionService sessions) =>
        {
            sessions.SignOut();
            return Results.Ok(new { SignedOut = true });
        });

        group.MapGet("/me", async (AuthSessionService sessions, CancellationToken cancellationToken) =>
        {
            var current = await sessions.GetCurrentUserAsync(cancellationToken);
            return current is null ? Results.Unauthorized() : Results.Ok(current);
        });

        group.MapGet("/me/history", async (
            int? limit,
            AuthSessionService sessions,
            IUserStore users,
            CancellationToken cancellationToken) =>
        {
            var current = await sessions.GetCurrentUserAsync(cancellationToken);
            if (current is null)
            {
                return Results.Unauthorized();
            }

            var saved = await users.ListSavedAnalysesAsync(
                current.UserId, Math.Clamp(limit ?? 50, 1, 100), cancellationToken);
            return Results.Ok(saved.Select(s => new
            {
                s.Id,
                s.Kind,
                s.ReferenceId,
                s.Title,
                s.Status,
                s.Version,
                s.CreatedAtUtc,
            }));
        });

        return routes;
    }
}

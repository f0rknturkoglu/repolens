using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RepoLens.Application.Identity;

namespace RepoLens.Api.Identity;

/// <summary>
/// Signed session cookie for same-origin deployments: the payload is
/// userId.githubId.login; a keyed HMAC signature prevents forgery. Cookies are
/// HttpOnly + SameSite=Lax and Secure unless the app runs in Development.
/// </summary>
public sealed class AuthCookieSettings
{
    public const string SectionName = "Auth";

    public string CookieName { get; set; } = "repolens.session";
    public string? CookieKey { get; set; }
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Auth stays off until an operator configures a signing key.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(CookieKey);
}

public sealed class AuthSessionService(
    AuthCookieSettings settings,
    IUserStore users,
    IHttpContextAccessor contextAccessor)
{
    public async Task<CurrentUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext;
        if (context is null || !settings.IsEnabled
            || !context.Request.Cookies.TryGetValue(settings.CookieName, out var cookie))
        {
            return null;
        }

        var (userId, githubId, login) = Unsign(cookie);
        if (userId is null)
        {
            return null;
        }

        var user = await users.GetByIdAsync(userId.Value, cancellationToken);
        return user is null
            ? null
            : new CurrentUser(user.Id, user.GitHubId, user.Login, user.Name, user.AvatarUrl);
    }

    public void SignIn(long userId, long gitHubId, string login)
    {
        var context = contextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        var cookie = Sign($"{userId}.{gitHubId}.{login}");
        var options = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !IsDevelopment(context),
            Expires = DateTimeOffset.UtcNow + settings.Lifetime,
            Path = "/",
        };
        context.Response.Cookies.Append(settings.CookieName, cookie, options);
    }

    public void SignOut()
    {
        contextAccessor.HttpContext?.Response.Cookies.Delete(settings.CookieName);
    }

    private string Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(settings.CookieKey!);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        var signature = Convert.ToBase64String(hmac.ComputeHash(data)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{payload}.{signature}";
    }

    private (long? UserId, long? GitHubId, string? Login) Unsign(string cookie)
    {
        var parts = cookie.Split('.');
        if (parts.Length < 4)
        {
            return (null, null, null);
        }

        var payload = string.Join('.', parts.Take(parts.Length - 1));
        var expected = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(cookie)))
        {
            return (null, null, null);
        }

        var values = payload.Split('.');
        return long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            && long.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out var githubId)
            ? (userId, githubId, values[2])
            : (null, null, null);
    }

    private static bool IsDevelopment(HttpContext context) =>
        string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);
}

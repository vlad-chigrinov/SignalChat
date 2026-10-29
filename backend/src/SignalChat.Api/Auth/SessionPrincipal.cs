using System.Globalization;
using System.Security.Claims;
using SignalChat.Domain.Users;

namespace SignalChat.Api.Auth;

/// <summary>
/// The claims stored in the session cookie. Deliberately minimal: no Google tokens, no email.
/// <see cref="ClaimTypes.NameIdentifier"/> also becomes SignalR's <c>Context.UserIdentifier</c>.
/// </summary>
public static class SessionPrincipal
{
    public static ClaimsPrincipal Create(User user, DateTimeOffset startedAt)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(SessionPolicy.SessionVersionClaim, user.SessionVersion.ToString(CultureInfo.InvariantCulture)),
                new Claim(SessionPolicy.SessionStartedClaim, startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ],
            AuthSchemes.Session);
        return new ClaimsPrincipal(identity);
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    /// <summary>Reads the session claims; false when any of them is missing or malformed.</summary>
    public static bool TryRead(ClaimsPrincipal principal, out SessionState session)
    {
        session = default;
        if (!TryGetUserId(principal, out var userId)
            || !int.TryParse(principal.FindFirstValue(SessionPolicy.SessionVersionClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || !long.TryParse(principal.FindFirstValue(SessionPolicy.SessionStartedClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var startedSeconds))
        {
            return false;
        }

        session = new SessionState(userId, version, DateTimeOffset.FromUnixTimeSeconds(startedSeconds));
        return true;
    }
}

using System.Security.Claims;
using SignalChat.Domain.Users;

namespace SignalChat.Api.Auth;

/// <summary>
/// The claims stored in the session cookie. Deliberately minimal: no Google tokens, no email.
/// <see cref="ClaimTypes.NameIdentifier"/> also becomes SignalR's <c>Context.UserIdentifier</c>.
/// </summary>
public static class SessionPrincipal
{
    public static ClaimsPrincipal Create(User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
            ],
            AuthSchemes.Session);
        return new ClaimsPrincipal(identity);
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

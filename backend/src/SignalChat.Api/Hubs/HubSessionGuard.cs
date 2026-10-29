using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Re-checks a connecting hub client's session against the database. The cookie was already validated on the
/// negotiate request; this closes the window in which a logout could land between negotiate and connect.
/// </summary>
public sealed class HubSessionGuard(IUserAccountService accounts, TimeProvider timeProvider)
{
    public async Task<bool> IsStillValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryRead(principal, out var session))
        {
            return false;
        }

        if (timeProvider.GetUtcNow() - session.StartedAt > SessionPolicy.AbsoluteSessionLifetime)
        {
            return false;
        }

        var storedVersion = await accounts.FindSessionVersionAsync(session.UserId, cancellationToken);
        return storedVersion == session.SessionVersion;
    }
}

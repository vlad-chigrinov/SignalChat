using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Auth;

/// <summary>
/// Runs on every request that carries the session cookie. The session is refused when its claims are
/// malformed, when its absolute lifetime has ended, when its user is gone, or when the user revoked it
/// (the stored <c>SessionVersion</c> moved on). There is no cache on purpose: an instant revocation
/// matters more than one indexed read per request.
/// </summary>
public static class SessionValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var http = context.HttpContext;
        if (!SessionPrincipal.TryRead(context.Principal!, out var session))
        {
            await RejectAsync(context);
            return;
        }

        var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (now - session.StartedAt > SessionPolicy.AbsoluteSessionLifetime)
        {
            await RejectAsync(context);
            return;
        }

        var accounts = http.RequestServices.GetRequiredService<IUserAccountService>();
        var storedVersion = await accounts.FindSessionVersionAsync(session.UserId, http.RequestAborted);
        if (storedVersion != session.SessionVersion)
        {
            await RejectAsync(context);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthSchemes.Session);
    }
}

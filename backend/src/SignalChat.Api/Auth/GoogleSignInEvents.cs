using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SignalChat.Api.Configuration;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Auth;

/// <summary>Turns a validated Google ID token into a SignalChat session.</summary>
public static class GoogleSignInEvents
{
    private const string SubjectClaim = "sub";
    private const string EmailClaim = "email";
    private const string EmailVerifiedClaim = "email_verified";
    private const string NameClaim = "name";
    private const string PictureClaim = "picture";
    private const string FailedSignInQuery = "?authError=google";

    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var claims = context.Principal!;
        var subject = claims.FindFirst(SubjectClaim)?.Value;
        var email = claims.FindFirst(EmailClaim)?.Value;
        var emailVerified = string.Equals(claims.FindFirst(EmailVerifiedClaim)?.Value, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email) || !emailVerified)
        {
            context.Fail("Google did not return a verified email for this account.");
            return;
        }

        var accounts = context.HttpContext.RequestServices.GetRequiredService<IUserAccountService>();
        var profile = new GoogleProfile(subject, email, claims.FindFirst(NameClaim)?.Value, claims.FindFirst(PictureClaim)?.Value);
        var user = await accounts.SignInWithGoogleAsync(profile, context.HttpContext.RequestAborted);

        // Replace Google's claims: the cookie carries only our own user id, display name and session claims.
        var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        context.Principal = SessionPrincipal.Create(user, now);
        context.Properties!.IsPersistent = true;
    }

    /// <summary>User cancelled consent or the callback was invalid: back to the SPA with a flag, no details.</summary>
    public static Task OnRemoteFailureAsync(RemoteFailureContext context)
    {
        var frontend = context.HttpContext.RequestServices.GetRequiredService<FrontendSettings>();
        context.Response.Redirect(new Uri(frontend.BaseUrl, "/" + FailedSignInQuery).ToString());
        context.HandleResponse();
        return Task.CompletedTask;
    }
}

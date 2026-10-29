using Microsoft.AspNetCore.Authentication.Cookies;

namespace SignalChat.Api.Auth;

public static class AuthSchemes
{
    /// <summary>The app session: an encrypted, HttpOnly cookie holding only the internal user id.</summary>
    public const string Session = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>Google OpenID Connect (Authorization Code + PKCE), used only to start a session.</summary>
    public const string Google = "Google";

    /// <summary>"__Host-" makes the browser enforce Secure, Path=/ and no Domain attribute.</summary>
    public const string SessionCookieName = "__Host-signalchat.session";

    public const string GoogleCallbackPath = "/auth/callback/google";
}

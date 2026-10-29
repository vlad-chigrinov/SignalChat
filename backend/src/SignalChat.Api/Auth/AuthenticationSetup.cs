using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SignalChat.Api.Auth;

public static class AuthenticationSetup
{
    private const string GoogleAuthority = "https://accounts.google.com";
    private const string GoogleDisplayName = "Google";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);

    public static IServiceCollection AddSessionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var google = GoogleSettings.FromConfiguration(configuration);
        services.AddSingleton(google);
        services.AddDataProtection().SetApplicationName("SignalChat");

        var authentication = services
            .AddAuthentication(AuthSchemes.Session)
            .AddCookie(AuthSchemes.Session, ConfigureSessionCookie);

        if (google.IsConfigured)
        {
            authentication.AddOpenIdConnect(AuthSchemes.Google, GoogleDisplayName, options => ConfigureGoogle(options, google));
        }

        services.AddAuthorization();
        return services;
    }

    private static void ConfigureSessionCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = AuthSchemes.SessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        // Strict: never sent on cross-site requests, the first CSRF barrier (OriginGuard is the second).
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = SessionLifetime;
        options.SlidingExpiration = true;

        // An API answers with status codes, never with a redirect to a login page.
        options.Events.OnRedirectToLogin = context => WriteStatus(context.Response, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => WriteStatus(context.Response, StatusCodes.Status403Forbidden);
    }

    private static void ConfigureGoogle(OpenIdConnectOptions options, GoogleSettings google)
    {
        options.Authority = GoogleAuthority;
        options.ClientId = google.ClientId;
        options.ClientSecret = google.ClientSecret;
        options.SignInScheme = AuthSchemes.Session;
        options.CallbackPath = AuthSchemes.GoogleCallbackPath;

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // Google tokens stay on the server for the duration of the callback only.
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;

        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

        options.Events.OnTokenValidated = GoogleSignInEvents.OnTokenValidatedAsync;
        options.Events.OnRemoteFailure = GoogleSignInEvents.OnRemoteFailureAsync;
    }

    private static Task WriteStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}

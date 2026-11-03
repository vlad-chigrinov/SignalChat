using Microsoft.AspNetCore.Authentication;

namespace SignalChat.Api.Auth;

/// <summary>
/// Google sign-in that ends on the Swagger page instead of the SPA, so the stage API can be tried from the browser.
/// Mapped only in Development. The destination is fixed, so there is no return-url parameter to abuse.
/// </summary>
public static class SwaggerLoginEndpoint
{
    public const string Path = "/auth/swagger-login";

    private const string SwaggerPagePath = "/swagger/index.html";

    public static IEndpointRouteBuilder MapSwaggerLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (GoogleSettings google) =>
            google.IsConfigured
                ? Results.Challenge(
                    new AuthenticationProperties { RedirectUri = SwaggerPagePath },
                    [AuthSchemes.Google])
                : Results.Problem(
                    title: "Google sign-in is not configured.",
                    detail: "Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in .env.",
                    statusCode: StatusCodes.Status503ServiceUnavailable))
            .ExcludeFromDescription();
        return endpoints;
    }
}

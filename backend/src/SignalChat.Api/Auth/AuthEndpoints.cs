using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SignalChat.Api.Configuration;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Auth;

public static class AuthEndpoints
{
    private const string NoStore = "no-store";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        // Top-level navigation (window.location), not fetch: it ends on Google's consent page.
        group.MapGet("/login", (string? returnUrl, GoogleSettings google, FrontendSettings frontend) =>
            google.IsConfigured
                ? Results.Challenge(
                    new AuthenticationProperties { RedirectUri = ReturnUrlPolicy.ToFrontendUrl(frontend, returnUrl) },
                    [AuthSchemes.Google])
                : Results.Problem(
                    title: "Google sign-in is not configured.",
                    detail: "Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in .env.",
                    statusCode: StatusCodes.Status503ServiceUnavailable));

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(AuthSchemes.Session);
            return Results.NoContent();
        });

        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal, IUserAccountService accounts, HttpContext http, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = NoStore;

        if (!SessionPrincipal.TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            // The account behind a still-valid cookie is gone: end the session.
            await http.SignOutAsync(AuthSchemes.Session);
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.AvatarUrl, user.Email));
    }
}

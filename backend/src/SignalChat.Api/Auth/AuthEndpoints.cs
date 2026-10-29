using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SignalChat.Api.Configuration;
using SignalChat.Api.Hubs;
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

        group.MapPost("/logout", LogoutAsync);

        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Revokes every session of the user (all devices), clears this cookie and cuts the live hub connections.
    /// A request without a valid session only clears the cookie.
    /// </summary>
    private static async Task<IResult> LogoutAsync(
        HttpContext http, IUserAccountService accounts, IHubConnectionRegistry hubs, CancellationToken cancellationToken)
    {
        if (SessionPrincipal.TryRead(http.User, out var session))
        {
            await accounts.RevokeSessionsAsync(session.UserId, cancellationToken);
            await http.SignOutAsync(AuthSchemes.Session);
            hubs.AbortAll(session.UserId.ToString());
        }
        else
        {
            await http.SignOutAsync(AuthSchemes.Session);
        }
        return Results.NoContent();
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

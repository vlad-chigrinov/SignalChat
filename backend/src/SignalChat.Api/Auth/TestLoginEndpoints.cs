using Microsoft.AspNetCore.Authentication;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Auth;

/// <summary>
/// The test-only sign-in route. It is mapped only when <see cref="E2eTestAuthSettings.Enabled"/>; otherwise the
/// route does not exist and answers 404. The session is issued by the same code as the Google callback.
/// </summary>
public static class TestLoginEndpoints
{
    public const string Route = "/auth/test-login";

    public static IEndpointRouteBuilder MapTestLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, LoginAsync);
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        TestLoginRequest? request, IUserAccountService accounts, HttpContext http, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (request is null || !TestHandle.IsValid(request.Handle))
        {
            return Results.Problem(
                title: "Invalid test handle.",
                detail: $"The handle must be 1 to {TestHandle.MaxLength} characters of a-z, 0-9, '_' or '-'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var handle = request.Handle!;
        var profile = new GoogleProfile(TestHandle.SubjectFor(handle), TestHandle.EmailFor(handle), handle, null);
        var user = await accounts.SignInWithGoogleAsync(profile, cancellationToken);

        // Same claims and same cookie as GoogleSignInEvents: the test session is indistinguishable from a real one.
        await http.SignInAsync(
            AuthSchemes.Session,
            SessionPrincipal.Create(user, clock.GetUtcNow()),
            new AuthenticationProperties { IsPersistent = true });

        return Results.NoContent();
    }
}

/// <summary>Body of <c>POST /auth/test-login</c>.</summary>
public sealed record TestLoginRequest(string? Handle);

using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

public static class UserSearchEndpoints
{
    public static RouteGroupBuilder MapUserSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/users/search", SearchAsync);
        return api;
    }

    private static async Task<IResult> SearchAsync(
        ClaimsPrincipal user, string? query, int? limit, IUserDirectory directory, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var pageSize = Pagination.Clamp(limit, ChatLimits.UserSearchDefaultLimit, ChatLimits.UserSearchMaxLimit);
        var users = await directory.SearchAsync(userId, query, pageSize, cancellationToken);
        return Results.Ok(users.Select(item => ChatMapping.ToDto(item)).ToList());
    }
}

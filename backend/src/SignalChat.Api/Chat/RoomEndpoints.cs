using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

public static class RoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/rooms");
        rooms.MapGet("/", ListMineAsync);
        rooms.MapGet("/discover", DiscoverAsync);
        rooms.MapPost("/", CreateAsync);
        rooms.MapPost("/{id:guid}/join", JoinAsync);
        rooms.MapPost("/{id:guid}/leave", LeaveAsync);
        return api;
    }

    private static async Task<IResult> ListMineAsync(ClaimsPrincipal user, IRoomService rooms, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var items = await rooms.ListMineAsync(userId, cancellationToken);
        return Results.Ok(items.Select(room => ChatMapping.ToDto(room)).ToList());
    }

    private static async Task<IResult> DiscoverAsync(
        ClaimsPrincipal user, string? query, int? limit, IRoomService rooms, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var pageSize = Pagination.Clamp(limit, ChatLimits.DiscoverDefaultLimit, ChatLimits.DiscoverMaxLimit);
        var items = await rooms.DiscoverAsync(userId, query, pageSize, cancellationToken);
        return Results.Ok(items.Select(room => ChatMapping.ToDto(room)).ToList());
    }

    private static async Task<IResult> CreateAsync(
        ClaimsPrincipal user, CreateRoomRequest request, IRoomService rooms, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var room = await rooms.CreateAsync(userId, request.Name, request.Description, cancellationToken);
        return Results.Json(ChatMapping.ToDto(room), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> JoinAsync(
        ClaimsPrincipal user, Guid id, IRoomService rooms, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        await rooms.JoinAsync(userId, id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LeaveAsync(
        ClaimsPrincipal user, Guid id, IRoomService rooms, HubGroupMembership hubGroups, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        await rooms.LeaveAsync(userId, id, cancellationToken);
        // A former member must stop receiving the room's live messages right away, not after a reconnect.
        await hubGroups.RemoveUserAsync(userId, new ConversationRef(ConversationKind.Room, id), cancellationToken);
        return Results.NoContent();
    }
}

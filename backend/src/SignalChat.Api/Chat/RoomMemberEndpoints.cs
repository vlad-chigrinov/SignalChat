using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Api.Hubs;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

/// <summary>The member list of a room, and invitations into it.</summary>
public static class RoomMemberEndpoints
{
    public static RouteGroupBuilder MapRoomMemberEndpoints(this RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/rooms");
        rooms.MapGet("/{id:guid}/members", ListAsync);
        rooms.MapPost("/{id:guid}/invitations", InviteAsync).RequireRateLimiting(ChatRateLimitPolicies.RoomInvite);
        return api;
    }

    /// <summary>Members only: anyone else gets the same "not found" as for a room that does not exist.</summary>
    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user, Guid id, IRoomMemberService members, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var items = await members.ListAsync(userId, id, ChatLimits.RoomMembersMaxCount, cancellationToken);
        return Results.Ok(items.Select(member => ChatMapping.ToDto(member)).ToList());
    }

    /// <summary>
    /// A member adds a chat partner, with no confirmation. The rules are in the service; after the change is stored the
    /// invitee is told in their personal group, and the room's open views get the "joined" notice.
    /// </summary>
    private static async Task<IResult> InviteAsync(
        ClaimsPrincipal user, Guid id, InviteToRoomRequest request, IRoomMemberService members, IRoomService rooms,
        RoomMembershipNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var added = await members.InviteAsync(userId, id, request.UserId, cancellationToken);
        var room = await rooms.FindMineAsync(request.UserId, id, cancellationToken);
        if (room is not null)
        {
            await notifier.AddedToRoomAsync(request.UserId, room);
        }
        await notifier.MemberJoinedAsync(id, added, cancellationToken);
        return Results.NoContent();
    }
}

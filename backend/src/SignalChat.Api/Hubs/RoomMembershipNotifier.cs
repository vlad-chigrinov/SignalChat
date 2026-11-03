using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Live events of a room's membership, called by the REST endpoints after the change is stored. The room's open views get
/// the "joined" notice; every member's personal group gets the newcomer's presence, because the two are peers from now on
/// and the presence snapshot they loaded earlier does not list each other.
/// </summary>
public sealed class RoomMembershipNotifier(
    IHubContext<ChatHub, IChatClient> hub, IRoomMemberService members, PresenceAnnouncer presence)
{
    public async Task MemberJoinedAsync(Guid roomId, RoomMemberAdded added, CancellationToken cancellationToken)
    {
        await hub.Clients.Group(ChatGroups.Room(roomId)).RoomMemberJoined(
            new RoomMemberJoinedDto(roomId, ChatMapping.ToDto(added.User), added.JoinedAt));

        var memberIds = await members.ListMemberIdsAsync(roomId, cancellationToken);
        var otherGroups = memberIds.Where(id => id != added.User.Id).Select(ChatGroups.User).ToList();
        if (otherGroups.Count > 0)
        {
            await hub.Clients.Groups(otherGroups).PresenceChanged(
                new PresenceChangedDto(added.User.Id, presence.IsOnline(added.User.Id)));
        }
    }

    public Task AddedToRoomAsync(Guid inviteeId, RoomSummary room) =>
        hub.Clients.Group(ChatGroups.User(inviteeId)).AddedToRoom(new AddedToRoomDto(ChatMapping.ToDto(room)));
}

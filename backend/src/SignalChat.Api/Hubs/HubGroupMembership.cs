using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Api.Chat.Dto;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Called when a user leaves a room over REST. Takes the user's open hub connections out of the room's group,
/// forgets the room on each connection (so a later join over REST followed by <c>JoinRoom</c> on the same
/// connection really joins again), and tells the remaining members with <c>UserLeft</c>. The leaver's own
/// connections are already out of the group, so they get nothing.
/// </summary>
public sealed class HubGroupMembership(
    IHubContext<ChatHub, IChatClient> hub,
    IHubConnectionRegistry connections,
    IUserDirectory users)
{
    public async Task RemoveUserAsync(Guid userId, ConversationRef conversation, CancellationToken cancellationToken)
    {
        var group = ChatGroups.For(conversation);
        foreach (var entry in connections.ConnectionsOf(userId.ToString()))
        {
            await hub.Groups.RemoveFromGroupAsync(entry.ConnectionId, group, cancellationToken);
            entry.Joined.TryRemove(conversation.Id, out _);
        }

        var user = await users.FindPublicAsync(userId, cancellationToken);
        if (user is not null)
        {
            await hub.Clients.Group(group).UserLeft(
                new ConversationMemberEventDto(conversation.Id, ChatMapping.ToDto(user)));
        }
    }
}

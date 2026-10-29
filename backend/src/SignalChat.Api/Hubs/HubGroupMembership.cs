using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Takes a user's open hub connections out of a conversation group. Called when the user leaves over REST:
/// otherwise a former member would keep receiving the room's messages until reconnecting.
/// </summary>
public sealed class HubGroupMembership(IHubContext<ChatHub, IChatClient> hub, IHubConnectionRegistry connections)
{
    public async Task RemoveUserAsync(Guid userId, ConversationRef conversation)
    {
        var group = ChatGroups.For(conversation);
        foreach (var connectionId in connections.ConnectionIds(userId.ToString()))
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group);
        }
    }
}

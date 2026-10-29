using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// The join path of the hub: membership is checked, the connection is added to the group, and membership is
/// checked once more. A REST leave that lands between the first check and the add is caught by the second one,
/// and the join is rolled back, so a connection never stays in a group it is no longer a member of.
/// </summary>
public sealed class ConversationJoin(IConversationAccess access)
{
    public async Task<ConversationRef> JoinAsync(
        Guid userId,
        Guid conversationId,
        string connectionId,
        IGroupManager groups,
        ConcurrentDictionary<Guid, ConversationRef> joined,
        CancellationToken cancellationToken)
    {
        var conversation = await access.FindForMemberAsync(userId, conversationId, cancellationToken)
            ?? throw HubErrors.NotFound();
        var group = ChatGroups.For(conversation);
        await groups.AddToGroupAsync(connectionId, group, cancellationToken);
        joined[conversationId] = conversation;

        if (await access.FindForMemberAsync(userId, conversationId, cancellationToken) is not null)
        {
            return conversation;
        }

        joined.TryRemove(conversationId, out _);
        await groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
        throw HubErrors.NotFound();
    }
}

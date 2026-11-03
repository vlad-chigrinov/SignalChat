using Microsoft.AspNetCore.SignalR;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Announces a stored reaction change: the counts to the whole conversation, then the actor's own choice to the
/// actor's connections. The order matters: a tab that receives both has the counts before it marks its own reaction.
/// </summary>
public sealed class ReactionNotifier(IHubContext<ChatHub, IChatClient> hub)
{
    public async Task NotifyAsync(Guid actorId, ReactionChange change, CancellationToken cancellationToken)
    {
        var conversationId = change.Conversation.Id;
        var counts = change.Reactions.Select(reaction => new ReactionCountDto(reaction.Emoji, reaction.Count)).ToList();

        await hub.Clients.Group(ChatGroups.For(change.Conversation))
            .ReactionsChanged(new ReactionsChangedEventDto(conversationId, change.MessageId, counts));
        await hub.Clients.Group(ChatGroups.User(actorId))
            .MyReactionChanged(new MyReactionChangedEventDto(conversationId, change.MessageId, change.MyEmoji));
    }
}

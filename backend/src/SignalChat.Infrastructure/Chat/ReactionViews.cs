using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

/// <summary>
/// One emoji under a message: how many people chose it and whether the viewer is one of them. It never says who
/// the others are.
/// </summary>
public sealed record ReactionView(string Emoji, int Count, bool ReactedByMe);

/// <summary>
/// The reactions of one message after a change, as the actor sees them, and where to announce it.
/// <see cref="MyEmoji"/> is the actor's reaction now, or null when they have none.
/// </summary>
public sealed record ReactionChange(ConversationRef Conversation, Guid MessageId, IReadOnlyList<ReactionView> Reactions)
{
    public string? MyEmoji => Reactions.FirstOrDefault(reaction => reaction.ReactedByMe)?.Emoji;
}

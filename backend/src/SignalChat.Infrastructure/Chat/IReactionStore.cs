using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

public interface IReactionStore
{
    /// <summary>
    /// The user picks <paramref name="emoji"/> for a message: no reaction yet → added; another one → replaced; the
    /// same one → removed. A user never holds more than one reaction per message.
    /// Throws <see cref="ChatRuleException"/>: "not_found" for a non-member, a message of another conversation, a
    /// missing or deleted message; "dm_not_accepted" for the initiator of a pending request; "invalid_reaction"
    /// for an emoji outside <see cref="ReactionCatalog"/>.
    /// </summary>
    Task<ReactionChange> ChooseAsync(
        Guid userId, Guid conversationId, Guid messageId, string? emoji, CancellationToken cancellationToken);
}

using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

public interface IMessageStore
{
    /// <summary>
    /// Saves a message from a member. Throws <see cref="ChatRuleException"/> with code "not_found" for a
    /// non-member (or a missing conversation) and "invalid_message" for a bad text. Used by the hub only.
    /// </summary>
    Task<MessageView> AppendAsync(Guid userId, Guid conversationId, string? text, CancellationToken cancellationToken);

    /// <summary>
    /// One page of visible history, newest page first in the query, returned oldest first. The caller
    /// must have checked membership (see <see cref="IConversationAccess"/>). Each message carries its reactions
    /// as counts, with the ones of <paramref name="viewerId"/> marked.
    /// </summary>
    Task<MessagePage> GetPageAsync(
        Guid viewerId, ConversationRef conversation, MessageCursor? before, int limit, CancellationToken cancellationToken);
}

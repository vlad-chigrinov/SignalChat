using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

/// <summary>
/// The membership checks of the chat: REST and the hub both go through them. A user who is not a member gets null,
/// the same answer as for an id that does not exist (no way to probe ids).
/// </summary>
public interface IConversationAccess
{
    /// <summary>
    /// Whether the user may see the conversation. A room member may; a direct participant may once the request is
    /// accepted, and the initiator also while it waits or was declined (so they can read their own request).
    /// </summary>
    Task<ConversationRef?> FindForMemberAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the user may exchange messages in the conversation right now: a room member, or a direct participant of an
    /// accepted conversation. Joining a group and sending both need this, not <see cref="FindForMemberAsync"/>.
    /// </summary>
    Task<ConversationRef?> FindActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
}

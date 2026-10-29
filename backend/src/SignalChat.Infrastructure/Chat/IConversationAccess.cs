using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

/// <summary>
/// The single membership check of the chat: REST and the hub both go through it. A user who is not a
/// member gets null, the same answer as for an id that does not exist (no way to probe ids).
/// </summary>
public interface IConversationAccess
{
    Task<ConversationRef?> FindForMemberAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
}

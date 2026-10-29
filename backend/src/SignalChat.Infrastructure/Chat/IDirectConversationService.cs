namespace SignalChat.Infrastructure.Chat;

public interface IDirectConversationService
{
    /// <summary>The caller's direct conversations, most recently active first.</summary>
    Task<IReadOnlyList<DirectConversationSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the conversation between the two users, creating it on first use. Throws "self_conversation"
    /// for the caller's own id and "not_found" for an unknown peer.
    /// </summary>
    Task<DirectConversationSummary> GetOrCreateAsync(Guid userId, Guid peerId, CancellationToken cancellationToken);
}

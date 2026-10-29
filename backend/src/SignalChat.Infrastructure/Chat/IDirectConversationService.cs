namespace SignalChat.Infrastructure.Chat;

public interface IDirectConversationService
{
    /// <summary>
    /// The caller's direct conversations, most recently active first. The initiator also sees its own requests
    /// (waiting or declined); the peer sees only accepted conversations.
    /// </summary>
    Task<IReadOnlyList<DirectConversationSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Asks to write to <paramref name="peerId"/>. Creates a request with <paramref name="text"/> as its first message,
    /// or reports an accepted conversation. Throws the dm_* codes of DirectRequestErrorCodes, "self_conversation",
    /// "not_found" (unknown peer) and "invalid_message" (text empty or too long).
    /// </summary>
    Task<DirectRequestOutcome> RequestAsync(Guid userId, Guid peerId, string? text, CancellationToken cancellationToken);

    /// <summary>Requests waiting for the caller's answer, newest first.</summary>
    Task<IReadOnlyList<IncomingDirectRequestView>> ListIncomingAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Accepts a request the caller received. Anyone else, or a request not waiting, gets "not_found".</summary>
    Task<DirectRequestResponse> AcceptAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Declines a request the caller received. Silent toward the initiator. Same "not_found" rules as accept.</summary>
    Task DeclineAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
}

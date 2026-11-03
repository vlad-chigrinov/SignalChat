using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

/// <summary>A saved photo message and the conversation it belongs to, so the caller can tell that conversation's members.</summary>
public sealed record PhotoMessageResult(MessageView Message, ConversationRef Conversation);

public interface IPhotoMessageService
{
    /// <summary>
    /// Saves a message of 1..9 photos with an optional caption from a member of an active conversation. The files are
    /// taken one at a time from <paramref name="uploads"/> (so only one is in memory), turned into stored pictures and
    /// written to the object store. If anything fails, in the files, the store or the database, whatever was already
    /// written to the store is removed and nothing of the message is kept.
    /// Throws <see cref="ChatRuleException"/>: "not_found" or "dm_not_accepted" for a caller who may not write here,
    /// "invalid_attachments", "invalid_image", "attachment_too_large", "invalid_message" and "storage_unavailable".
    /// </summary>
    Task<PhotoMessageResult> SendAsync(
        Guid userId, Guid conversationId, string? caption, IAsyncEnumerable<PhotoUpload> uploads, CancellationToken cancellationToken);
}

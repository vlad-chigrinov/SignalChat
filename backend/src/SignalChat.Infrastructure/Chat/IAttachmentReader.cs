using SignalChat.Infrastructure.Media;

namespace SignalChat.Infrastructure.Chat;

public enum AttachmentVariant
{
    /// <summary>The picture as stored (at most 2560 px on its longer side).</summary>
    Original,

    /// <summary>The small picture the feed shows.</summary>
    Thumbnail,
}

/// <summary>An opened photo file with what the answer needs to describe it. The caller disposes <see cref="Content"/>.</summary>
public sealed record AttachmentFile(MediaObject Content, string ContentType, string FileName);

public interface IAttachmentReader
{
    /// <summary>
    /// Opens a photo for a user who may see its conversation (a room member, or a participant of a direct conversation).
    /// Anyone else, a deleted message and an unknown id all give null: the same answer, so ids cannot be probed.
    /// Throws <see cref="MediaStorageException"/> when the store fails.
    /// </summary>
    Task<AttachmentFile?> OpenAsync(Guid userId, Guid attachmentId, AttachmentVariant variant, CancellationToken cancellationToken);
}

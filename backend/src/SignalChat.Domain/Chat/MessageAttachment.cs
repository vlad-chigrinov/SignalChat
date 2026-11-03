namespace SignalChat.Domain.Chat;

/// <summary>
/// One photo of a message. The picture and its thumbnail live in the object store under the two keys; this row holds
/// only what the chat needs to lay the photo out and to find it again. <see cref="Position"/> is its place in the message.
/// </summary>
public sealed class MessageAttachment
{
    private MessageAttachment()
    {
        // Required by EF Core.
        OriginalKey = string.Empty;
        ThumbnailKey = string.Empty;
        ContentType = string.Empty;
        FileName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public int Position { get; private set; }

    public string OriginalKey { get; private set; }

    public string ThumbnailKey { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int ThumbnailWidth { get; private set; }

    public int ThumbnailHeight { get; private set; }

    public long SizeBytes { get; private set; }

    public string ContentType { get; private set; }

    /// <summary>The name the picture is downloaded under: the uploaded name, made safe, with the extension of the stored type.</summary>
    public string FileName { get; private set; }

    internal static MessageAttachment From(Guid messageId, int position, PhotoDraft draft) => new()
    {
        Id = draft.Id,
        MessageId = messageId,
        Position = position,
        OriginalKey = draft.OriginalKey,
        ThumbnailKey = draft.ThumbnailKey,
        Width = draft.Width,
        Height = draft.Height,
        ThumbnailWidth = draft.ThumbnailWidth,
        ThumbnailHeight = draft.ThumbnailHeight,
        SizeBytes = draft.SizeBytes,
        ContentType = draft.ContentType,
        FileName = draft.FileName,
    };
}

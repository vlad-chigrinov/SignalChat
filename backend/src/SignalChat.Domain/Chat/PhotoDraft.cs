namespace SignalChat.Domain.Chat;

/// <summary>
/// A processed photo whose files are already in the object store, waiting to become an attachment of a new message.
/// The keys name the stored objects; the sizes are those of the stored (re-encoded) pictures.
/// </summary>
public sealed record PhotoDraft(
    Guid Id,
    string OriginalKey,
    string ThumbnailKey,
    int Width,
    int Height,
    int ThumbnailWidth,
    int ThumbnailHeight,
    long SizeBytes,
    string ContentType,
    string FileName);

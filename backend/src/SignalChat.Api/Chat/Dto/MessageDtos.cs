namespace SignalChat.Api.Chat.Dto;

/// <summary>
/// One photo of a message. <see cref="Url"/> is the picture and <see cref="ThumbnailUrl"/> the small one for the feed, both
/// paths on this API that need the session (a photo is never public). The sizes are in pixels and let the client reserve the
/// space before the picture arrives.
/// </summary>
public sealed record AttachmentDto(
    Guid Id,
    int Width,
    int Height,
    int ThumbnailWidth,
    int ThumbnailHeight,
    long SizeBytes,
    string ContentType,
    string FileName,
    string Url,
    string ThumbnailUrl);

/// <summary>A message. <see cref="Attachments"/> is empty for a text and has 1..9 photos for a photo message (the text is then its caption).</summary>
public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    UserSummaryDto Author,
    string Text,
    DateTimeOffset SentAt,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary>One history page, oldest first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, string? NextCursor);

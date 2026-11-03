namespace SignalChat.Domain.Chat;

/// <summary>
/// A message in exactly one conversation: a room (<see cref="RoomId"/>) or a direct conversation
/// (<see cref="DirectConversationId"/>). It is a text, or photos with an optional caption. Deleting is soft: a deleted
/// message is hidden from history.
/// </summary>
public sealed class Message
{
    public const int MaxTextLength = 2000;
    public const int MaxPhotos = 9;

    private readonly List<MessageAttachment> _attachments = [];

    private Message()
    {
        // Required by EF Core.
        Text = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid? RoomId { get; private set; }

    public Guid? DirectConversationId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Text { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>The photos of the message in display order; empty for a text message.</summary>
    public IReadOnlyList<MessageAttachment> Attachments => _attachments;

    /// <summary>The room or direct conversation this message belongs to.</summary>
    public Guid ConversationId => RoomId ?? DirectConversationId ?? Guid.Empty;

    /// <summary>A text, or with <paramref name="photos"/> a photo message whose caption (the text) may be empty.</summary>
    public static Message InRoom(
        Guid roomId, Guid authorId, string? text, DateTimeOffset now, IReadOnlyList<PhotoDraft>? photos = null) =>
        Create(roomId, null, authorId, text, now, photos);

    public static Message InDirectConversation(
        Guid directConversationId, Guid authorId, string? text, DateTimeOffset now, IReadOnlyList<PhotoDraft>? photos = null) =>
        Create(null, directConversationId, authorId, text, now, photos);

    /// <summary>Soft delete: the message stays in storage and disappears from history. Idempotent.</summary>
    public void Delete(DateTimeOffset now) => DeletedAt ??= now;

    private static Message Create(
        Guid? roomId, Guid? directConversationId, Guid authorId, string? text, DateTimeOffset now, IReadOnlyList<PhotoDraft>? photos)
    {
        var photoCount = photos?.Count ?? 0;
        if (photos is not null && (photoCount == 0 || photoCount > MaxPhotos))
        {
            throw new ChatRuleException(AttachmentErrorCodes.InvalidAttachments, $"A message carries 1..{MaxPhotos} photos.");
        }

        var trimmed = (text ?? string.Empty).Trim();
        var minTextLength = photoCount > 0 ? 0 : 1;
        if (trimmed.Length < minTextLength || trimmed.Length > MaxTextLength)
        {
            throw new ChatRuleException("invalid_message", $"Message must be {minTextLength}..{MaxTextLength} characters after trimming.");
        }

        var message = new Message
        {
            Id = Guid.CreateVersion7(now),
            RoomId = roomId,
            DirectConversationId = directConversationId,
            AuthorId = authorId,
            Text = trimmed,
            CreatedAt = now,
        };
        for (var position = 0; position < photoCount; position++)
        {
            message._attachments.Add(MessageAttachment.From(message.Id, position, photos![position]));
        }
        return message;
    }
}

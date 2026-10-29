namespace SignalChat.Domain.Chat;

/// <summary>
/// A text message in exactly one conversation: a room (<see cref="RoomId"/>) or a direct conversation
/// (<see cref="DirectConversationId"/>). Deleting is soft: a deleted message is hidden from history.
/// </summary>
public sealed class Message
{
    public const int MaxTextLength = 2000;

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

    /// <summary>The room or direct conversation this message belongs to.</summary>
    public Guid ConversationId => RoomId ?? DirectConversationId ?? Guid.Empty;

    public static Message InRoom(Guid roomId, Guid authorId, string? text, DateTimeOffset now) =>
        Create(roomId, null, authorId, text, now);

    public static Message InDirectConversation(Guid directConversationId, Guid authorId, string? text, DateTimeOffset now) =>
        Create(null, directConversationId, authorId, text, now);

    /// <summary>Soft delete: the message stays in storage and disappears from history. Idempotent.</summary>
    public void Delete(DateTimeOffset now) => DeletedAt ??= now;

    private static Message Create(Guid? roomId, Guid? directConversationId, Guid authorId, string? text, DateTimeOffset now)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxTextLength)
        {
            throw new ChatRuleException("invalid_message", $"Message must be 1..{MaxTextLength} characters after trimming.");
        }

        return new Message
        {
            Id = Guid.CreateVersion7(now),
            RoomId = roomId,
            DirectConversationId = directConversationId,
            AuthorId = authorId,
            Text = trimmed,
            CreatedAt = now,
        };
    }
}

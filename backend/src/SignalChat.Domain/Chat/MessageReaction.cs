namespace SignalChat.Domain.Chat;

/// <summary>
/// One user's reaction to one message. The pair (message, user) is the key, so a user holds at most one reaction
/// per message: choosing another emoji changes this row instead of adding a second one.
/// </summary>
public sealed class MessageReaction
{
    public const string InvalidReactionCode = "invalid_reaction";

    private MessageReaction()
    {
        // Required by EF Core.
        Emoji = string.Empty;
    }

    public Guid MessageId { get; private set; }

    public Guid UserId { get; private set; }

    public string Emoji { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MessageReaction Create(Guid messageId, Guid userId, string? emoji, DateTimeOffset now) =>
        new()
        {
            MessageId = messageId,
            UserId = userId,
            Emoji = Validated(emoji),
            CreatedAt = now,
        };

    /// <summary>Replaces the emoji; the old one stays when the new one is refused.</summary>
    public void ChangeTo(string? emoji) => Emoji = Validated(emoji);

    private static string Validated(string? emoji) =>
        ReactionCatalog.IsAllowed(emoji)
            ? emoji!
            : throw new ChatRuleException(InvalidReactionCode, "The reaction is not one of the allowed emoji.");
}

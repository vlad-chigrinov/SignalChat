namespace SignalChat.Domain.Chat;

/// <summary>
/// The only reactions a message accepts. Anything else is refused on the server, so a client cannot store free text
/// or markup as a "reaction". The order is the display order. Mirrored by <c>frontend/src/api/reactionCatalog.ts</c>.
/// </summary>
public static class ReactionCatalog
{
    public const int MaxEmojiLength = 16;

    public static IReadOnlyList<string> Emojis { get; } = ["👍", "❤️", "😂", "😮", "😢", "🎉"];

    public static bool IsAllowed(string? emoji) => emoji is not null && Emojis.Contains(emoji, StringComparer.Ordinal);

    /// <summary>Position in the display order; unknown emoji sort last.</summary>
    public static int OrderOf(string emoji)
    {
        for (var index = 0; index < Emojis.Count; index++)
        {
            if (string.Equals(Emojis[index], emoji, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return Emojis.Count;
    }
}

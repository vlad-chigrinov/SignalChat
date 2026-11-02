using System.Globalization;
using SignalChat.Domain.Chat;

namespace SignalChat.Domain.Users;

/// <summary>
/// What a name the user types may look like: not empty after trimming, within <see cref="User.DisplayNameMaxLength"/>,
/// and free of characters that break a layout or fake another name. A zero-width joiner stays allowed: emoji need it.
/// </summary>
internal static class DisplayNamePolicy
{
    private const char BidiEmbeddingFirst = '‪';
    private const char BidiEmbeddingLast = '‮';
    private const char BidiIsolateFirst = '⁦';
    private const char BidiIsolateLast = '⁩';

    public static string Validate(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw Refused("The name cannot be empty.");
        }
        if (trimmed.Length > User.DisplayNameMaxLength)
        {
            throw Refused($"The name cannot be longer than {User.DisplayNameMaxLength} characters.");
        }
        if (trimmed.Any(IsForbidden))
        {
            throw Refused("The name contains characters that are not allowed.");
        }
        return trimmed;
    }

    /// <summary>Control characters, line and paragraph separators, and the bidirectional overrides that reorder text.</summary>
    private static bool IsForbidden(char character) =>
        char.IsControl(character)
        || CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        || character is >= BidiEmbeddingFirst and <= BidiEmbeddingLast
        || character is >= BidiIsolateFirst and <= BidiIsolateLast;

    private static ChatRuleException Refused(string message) => new(UserProfileErrorCodes.InvalidDisplayName, message);
}

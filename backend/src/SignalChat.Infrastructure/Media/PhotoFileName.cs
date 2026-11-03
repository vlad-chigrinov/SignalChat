using System.Text;

namespace SignalChat.Infrastructure.Media;

/// <summary>
/// The name a stored photo is downloaded under. It comes from the uploaded name, which is the client's text and nothing
/// the server can trust: the path, control characters and characters that a file system or a header would mind are
/// dropped, the length is bounded, and the extension is that of the type the server actually stored.
/// </summary>
public static class PhotoFileName
{
    public const int MaxStemLength = 80;
    public const string FallbackStem = "photo";

    private const char Replacement = '_';
    private static readonly char[] PathSeparators = ['/', '\\'];
    private static readonly char[] Forbidden = ['<', '>', ':', '"', '|', '?', '*'];

    public static string Normalize(string? uploadedName)
    {
        var name = uploadedName ?? string.Empty;
        var lastSeparator = name.LastIndexOfAny(PathSeparators);
        var leaf = lastSeparator >= 0 ? name[(lastSeparator + 1)..] : name;

        var extensionStart = leaf.LastIndexOf('.');
        var stem = extensionStart > 0 ? leaf[..extensionStart] : leaf;

        var cleaned = new StringBuilder(stem.Length);
        foreach (var character in stem.Trim())
        {
            cleaned.Append(char.IsControl(character) || Array.IndexOf(Forbidden, character) >= 0 ? Replacement : character);
        }

        var result = cleaned.ToString().Trim(' ', '.');
        if (result.Length > MaxStemLength)
        {
            result = result[..MaxStemLength].TrimEnd(' ', '.');
        }
        return (result.Length == 0 ? FallbackStem : result) + PhotoLimits.StoredExtension;
    }
}

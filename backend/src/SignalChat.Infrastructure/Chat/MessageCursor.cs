using System.Globalization;
using System.Text;

namespace SignalChat.Infrastructure.Chat;

/// <summary>
/// Position in a message history: the (sent time, id) of the oldest message already read.
/// It is handed to the client as an opaque base64url string, "ticks:guid".
/// </summary>
public readonly record struct MessageCursor(DateTimeOffset SentAt, Guid Id)
{
    private const char Separator = ':';
    private const int TicksPart = 0;
    private const int IdPart = 1;
    private const int PartCount = 2;
    private const char Base64Plus = '+';
    private const char Base64Slash = '/';
    private const char Base64UrlPlus = '-';
    private const char Base64UrlSlash = '_';
    private const char Base64Padding = '=';
    private const int Base64BlockSize = 4;

    public string Encode()
    {
        var plain = string.Create(CultureInfo.InvariantCulture, $"{SentAt.UtcTicks}{Separator}{Id:D}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plain))
            .TrimEnd(Base64Padding)
            .Replace(Base64Plus, Base64UrlPlus)
            .Replace(Base64Slash, Base64UrlSlash);
    }

    /// <summary>Reads a cursor produced by <see cref="Encode"/>. False for anything else.</summary>
    public static bool TryParse(string? value, out MessageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string plain;
        try
        {
            plain = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(value)));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = plain.Split(Separator);
        if (parts.Length != PartCount
            || !long.TryParse(parts[TicksPart], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTimeOffset.MinValue.UtcTicks
            || ticks > DateTimeOffset.MaxValue.UtcTicks
            || !Guid.TryParseExact(parts[IdPart], "D", out var id))
        {
            return false;
        }

        cursor = new MessageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }

    private static string PadBase64(string base64Url)
    {
        var padded = base64Url.Replace(Base64UrlPlus, Base64Plus).Replace(Base64UrlSlash, Base64Slash);
        var remainder = padded.Length % Base64BlockSize;
        return remainder == 0 ? padded : padded.PadRight(padded.Length + Base64BlockSize - remainder, Base64Padding);
    }
}

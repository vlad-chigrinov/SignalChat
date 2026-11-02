namespace SignalChat.Infrastructure.Users;

/// <summary>Bounds on an uploaded avatar: what the server reads, and what it keeps.</summary>
public static class AvatarLimits
{
    private const int BytesPerMebibyte = 1024 * 1024;

    /// <summary>Largest upload read from a request. A phone photo fits; anything bigger is refused unread.</summary>
    public const int MaxUploadBytes = 2 * BytesPerMebibyte;

    /// <summary>Longest side of an uploaded picture. The check uses the header, so a huge picture is never decoded.</summary>
    public const int MaxSourceSidePx = 4096;

    /// <summary>The stored avatar is a square of this many pixels; chat shows it at 72 px at most, so 256 stays sharp on dense screens.</summary>
    public const int OutputSidePx = 256;

    public const int WebpQuality = 85;

    public const string StoredContentType = "image/webp";
}

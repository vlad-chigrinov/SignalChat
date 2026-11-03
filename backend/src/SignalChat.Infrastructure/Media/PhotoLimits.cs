namespace SignalChat.Infrastructure.Media;

/// <summary>Bounds on photos in a message: what the server reads, what it decodes, and what it keeps.</summary>
public static class PhotoLimits
{
    private const int BytesPerMebibyte = 1024 * 1024;

    /// <summary>Largest single file read from a request. A phone photo fits; anything bigger is refused.</summary>
    public const int MaxFileBytes = 12 * BytesPerMebibyte;

    /// <summary>Largest sum of all files of one message.</summary>
    public const int MaxTotalBytes = 48 * BytesPerMebibyte;

    /// <summary>Room for the multipart framing and the caption on top of the files, so the body limit never cuts a valid upload.</summary>
    public const int RequestOverheadBytes = 1 * BytesPerMebibyte;

    public const int MaxRequestBodyBytes = MaxTotalBytes + RequestOverheadBytes;

    /// <summary>Longest side of an uploaded picture. The check uses the header, so a huge picture is never decoded.</summary>
    public const int MaxSourceSidePx = 12000;

    /// <summary>Most pixels an uploaded picture may have (a 50-megapixel phone photo fits). Also read from the header.</summary>
    public const int MaxSourcePixels = 100_000_000;

    /// <summary>
    /// Most pixels the server decodes for one picture, after a codec that can scale has scaled it down on the way
    /// (JPEG does). Four bytes each, so this is the native memory of one decode, and it is the reason for the memory limit of the API.
    /// </summary>
    public const int MaxDecodedPixels = 16_000_000;

    /// <summary>The stored picture is at most this many pixels on its longer side; larger photos are scaled down.</summary>
    public const int StoredMaxSidePx = 2560;

    /// <summary>The thumbnail in the feed is at most this many pixels on its longer side (a large single photo on a dense screen).</summary>
    public const int ThumbnailMaxSidePx = 640;

    public const int StoredWebpQuality = 90;

    public const int ThumbnailWebpQuality = 80;

    public const string StoredContentType = "image/webp";

    public const string StoredExtension = ".webp";
}

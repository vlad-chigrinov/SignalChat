namespace SignalChat.Infrastructure.Media;

/// <summary>A photo ready to be stored: the re-encoded picture and its thumbnail, both WebP, with their pixel sizes.</summary>
public sealed record ProcessedPhoto(
    byte[] Original,
    int Width,
    int Height,
    byte[] Thumbnail,
    int ThumbnailWidth,
    int ThumbnailHeight);

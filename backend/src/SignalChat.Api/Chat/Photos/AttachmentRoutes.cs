using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat.Photos;

/// <summary>The paths of photo files on this API. Built only here, so the format lives in one place.</summary>
public static class AttachmentRoutes
{
    public const string OriginalSegment = "original";
    public const string ThumbnailSegment = "thumbnail";
    public const string DownloadQuery = "download";

    public static string PathOf(Guid attachmentId, AttachmentVariant variant) =>
        $"{ChatEndpoints.ApiPrefix}/attachments/{attachmentId}/{(variant == AttachmentVariant.Thumbnail ? ThumbnailSegment : OriginalSegment)}";

    public static bool TryParseVariant(string segment, out AttachmentVariant variant)
    {
        variant = segment switch
        {
            OriginalSegment => AttachmentVariant.Original,
            ThumbnailSegment => AttachmentVariant.Thumbnail,
            _ => default,
        };
        return segment is OriginalSegment or ThumbnailSegment;
    }
}

namespace SignalChat.Infrastructure.Media;

public interface IPhotoImageProcessor
{
    /// <summary>
    /// Turns an upload into the stored photo and its thumbnail: WebP written from the decoded pixels alone, turned upright by
    /// the EXIF orientation, with no metadata (no location, no camera) and no trailing bytes. The type is decided by the
    /// content, never by a name or a media type. Calls run one at a time, so the native memory of the decoder stays bounded.
    /// Throws <c>ChatRuleException</c> with <c>invalid_image</c>.
    /// </summary>
    Task<ProcessedPhoto> ProcessAsync(ReadOnlyMemory<byte> upload, CancellationToken cancellationToken);
}

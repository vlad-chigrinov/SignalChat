using SignalChat.Domain.Chat;
using SkiaSharp;

namespace SignalChat.Infrastructure.Media;

/// <summary>
/// Decodes the upload and writes new pictures from its pixels. Metadata (EXIF with the location included), trailing bytes
/// and anything else that is not a pixel cannot reach the stored files, so an upload that is also a script or an archive
/// stays harmless. Memory is the constraint: one upload is decoded at a time (the gate), a codec that can scale decodes
/// straight to the size that is needed, and the turn upright happens while drawing, not as a copy of the whole picture.
/// </summary>
public sealed class PhotoImageProcessor : IPhotoImageProcessor
{
    private static readonly SKEncodedImageFormat[] AllowedFormats =
        [SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Gif, SKEncodedImageFormat.Webp];

    private static readonly SKSamplingOptions Downscale = new(SKCubicResampler.Mitchell);
    private static readonly SKSamplingOptions DownscaleSmooth = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private const float JpegScaleSteps = 8f;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ProcessedPhoto> ProcessAsync(ReadOnlyMemory<byte> upload, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Process(upload);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProcessedPhoto Process(ReadOnlyMemory<byte> upload)
    {
        // The codec recognises the format from the content and reads the size from the header, before any pixel is decoded.
        using var data = SKData.CreateCopy(upload.Span);
        using var codec = SKCodec.Create(data);
        if (codec is null || !AllowedFormats.Contains(codec.EncodedFormat) || !HeaderWithinLimits(codec.Info))
        {
            throw Invalid();
        }

        var decodedSize = codec.GetScaledDimensions(DesiredScale(codec.Info));
        if ((long)decodedSize.Width * decodedSize.Height > PhotoLimits.MaxDecodedPixels)
        {
            throw Invalid();
        }

        using var decoded = Decode(codec, decodedSize);
        using var upright = DrawUpright(decoded, codec.EncodedOrigin);
        using var thumbnail = DrawThumbnail(upright);
        return new ProcessedPhoto(
            Encode(upright, PhotoLimits.StoredWebpQuality), upright.Width, upright.Height,
            Encode(thumbnail, PhotoLimits.ThumbnailWebpQuality), thumbnail.Width, thumbnail.Height);
    }

    private static bool HeaderWithinLimits(SKImageInfo info) =>
        info.Width > 0
        && info.Height > 0
        && info.Width <= PhotoLimits.MaxSourceSidePx
        && info.Height <= PhotoLimits.MaxSourceSidePx
        && (long)info.Width * info.Height <= PhotoLimits.MaxSourcePixels;

    // JPEG decodes at k/8 of its size for free. The smallest k that still reaches the stored size is asked for, so the draw
    // below only ever scales down. Other codecs answer 1 and decode in full.
    private static float DesiredScale(SKImageInfo info)
    {
        var scale = PhotoLimits.StoredMaxSidePx / (float)Math.Max(info.Width, info.Height);
        return Math.Min(1f, MathF.Ceiling(scale * JpegScaleSteps) / JpegScaleSteps);
    }

    // A GIF photo is its first frame; the codec decodes frame 0 unless asked for another.
    private static SKBitmap Decode(SKCodec codec, SKSizeI size)
    {
        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw Invalid();
        }
        bitmap.SetImmutable();
        return bitmap;
    }

    /// <summary>The picture turned upright and scaled down to the stored size in one draw; it is never scaled up.</summary>
    private static SKImage DrawUpright(SKBitmap decoded, SKEncodedOrigin origin)
    {
        var turnsQuarter = ImageOrientation.TurnsQuarter(origin);
        var uprightWidth = turnsQuarter ? decoded.Height : decoded.Width;
        var uprightHeight = turnsQuarter ? decoded.Width : decoded.Height;
        var target = FitInside(uprightWidth, uprightHeight, PhotoLimits.StoredMaxSidePx);

        var turn = ImageOrientation.MatrixFor(origin, decoded.Width, decoded.Height);
        var scale = SKMatrix.CreateScale(target.Width / (float)uprightWidth, target.Height / (float)uprightHeight);
        using var surface = SKSurface.Create(new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var source = SKImage.FromBitmap(decoded);
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.SetMatrix(SKMatrix.Concat(scale, turn));
        surface.Canvas.DrawImage(source, 0, 0, Downscale);
        return surface.Snapshot();
    }

    private static SKImage DrawThumbnail(SKImage upright)
    {
        var target = FitInside(upright.Width, upright.Height, PhotoLimits.ThumbnailMaxSidePx);
        using var surface = SKSurface.Create(new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(upright, SKRect.Create(target.Width, target.Height), DownscaleSmooth);
        return surface.Snapshot();
    }

    private static byte[] Encode(SKImage image, int quality)
    {
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality);
        return encoded.ToArray();
    }

    private static SKSizeI FitInside(int width, int height, int maxSide)
    {
        var longSide = Math.Max(width, height);
        if (longSide <= maxSide)
        {
            return new SKSizeI(width, height);
        }

        var ratio = maxSide / (double)longSide;
        return new SKSizeI(Math.Max(1, (int)Math.Round(width * ratio)), Math.Max(1, (int)Math.Round(height * ratio)));
    }

    private static ChatRuleException Invalid() =>
        new(AttachmentErrorCodes.InvalidImage, "The file is not a supported picture (JPEG, PNG, GIF or WebP).");
}

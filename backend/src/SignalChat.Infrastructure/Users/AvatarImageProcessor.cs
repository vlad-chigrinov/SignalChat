using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Media;
using SkiaSharp;

namespace SignalChat.Infrastructure.Users;

/// <summary>
/// Decodes the upload and writes a new picture from its pixels. Metadata, trailing bytes and anything else that is not
/// a pixel cannot reach the stored file, so an upload that is also a script or an archive stays harmless.
/// </summary>
public sealed class AvatarImageProcessor : IAvatarImageProcessor
{
    private static readonly SKEncodedImageFormat[] AllowedFormats =
        [SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Gif, SKEncodedImageFormat.Webp];

    public byte[] Process(byte[] upload)
    {
        if (upload.Length > AvatarLimits.MaxUploadBytes)
        {
            throw new ChatRuleException(UserProfileErrorCodes.AvatarTooLarge, "The picture is too large.");
        }

        // The codec recognises the format from the content and reads the size from the header, before any pixel is decoded.
        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data);
        if (codec is null
            || !AllowedFormats.Contains(codec.EncodedFormat)
            || codec.Info.Width > AvatarLimits.MaxSourceSidePx
            || codec.Info.Height > AvatarLimits.MaxSourceSidePx)
        {
            throw Invalid();
        }

        using var decoded = Decode(codec);
        using var oriented = ImageOrientation.Apply(decoded, codec.EncodedOrigin);
        return CropToSquareAndEncode(oriented);
    }

    // A GIF avatar is its first frame; the codec decodes frame 0 unless asked for another.
    private static SKBitmap Decode(SKCodec codec)
    {
        var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw Invalid();
        }
        return bitmap;
    }

    private static byte[] CropToSquareAndEncode(SKBitmap picture)
    {
        var side = Math.Min(picture.Width, picture.Height);
        var source = SKRect.Create((picture.Width - side) / 2f, (picture.Height - side) / 2f, side, side);
        var target = SKRect.Create(AvatarLimits.OutputSidePx, AvatarLimits.OutputSidePx);

        using var surface = SKSurface.Create(new SKImageInfo(
            AvatarLimits.OutputSidePx, AvatarLimits.OutputSidePx, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var image = SKImage.FromBitmap(picture);
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(image, source, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Webp, AvatarLimits.WebpQuality);
        return encoded.ToArray();
    }

    private static ChatRuleException Invalid() =>
        new(UserProfileErrorCodes.InvalidAvatar, "The file is not a supported picture (JPEG, PNG, GIF or WebP).");
}

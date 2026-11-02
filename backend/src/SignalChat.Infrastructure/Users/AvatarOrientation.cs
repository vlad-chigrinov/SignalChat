using SkiaSharp;

namespace SignalChat.Infrastructure.Users;

/// <summary>Turns a picture the way its EXIF orientation says, so a phone photo taken upright is not stored on its side.</summary>
internal static class AvatarOrientation
{
    /// <summary>A new bitmap with the orientation applied; the caller disposes it.</summary>
    public static SKBitmap Apply(SKBitmap source, SKEncodedOrigin origin)
    {
        var turnsQuarter = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = turnsQuarter ? source.Height : source.Width;
        var height = turnsQuarter ? source.Width : source.Height;

        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(MatrixFor(origin, source.Width, source.Height));
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    // Skia's own table (SkEncodedOriginToMatrix): where a source pixel (x, y) lands in the upright picture.
    private static SKMatrix MatrixFor(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}

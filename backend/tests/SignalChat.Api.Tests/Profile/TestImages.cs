using SkiaSharp;

namespace SignalChat.Api.Tests.Profile;

/// <summary>Small generated pictures for the avatar tests, so no binary fixtures live in the repository.</summary>
internal static class TestImages
{
    private const int EncodeQuality = 90;

    // A 1x1 GIF: Skia decodes GIF but cannot write one.
    private static readonly byte[] TinyGif =
        Convert.FromBase64String("R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==");

    public static byte[] Png(int width = 300, int height = 200) => Encode(Solid(width, height), SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width = 300, int height = 200) => Encode(Solid(width, height), SKEncodedImageFormat.Jpeg);

    public static byte[] Webp(int width = 300, int height = 200) => Encode(Solid(width, height), SKEncodedImageFormat.Webp);

    public static byte[] Gif() => TinyGif;

    /// <summary>A picture split into vertical bands of the given colours, left to right.</summary>
    public static byte[] Bands(SKEncodedImageFormat format, int width, int height, params SKColor[] bands)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            for (var index = 0; index < bands.Length; index++)
            {
                using var paint = new SKPaint { Color = bands[index] };
                canvas.DrawRect(width * index / bands.Length, 0, width / (float)bands.Length + 1, height, paint);
            }
        }
        return Encode(bitmap, format);
    }

    /// <summary>The JPEG with an EXIF segment that carries only the orientation tag (1..8).</summary>
    public static byte[] WithExifOrientation(byte[] jpeg, byte orientation)
    {
        byte[] segment =
        [
            0xFF, 0xE1, 0x00, 0x22, // APP1, 34 bytes with this length field
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian TIFF header, IFD at 8
            0x00, 0x01, // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00, // Orientation, SHORT
            0x00, 0x00, 0x00, 0x00, // no next IFD
        ];
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    private static SKBitmap Solid(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(200, 40, 40));
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, EncodeQuality);
            return data.ToArray();
        }
    }
}

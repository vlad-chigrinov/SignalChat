using System.Text;
using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Users;
using SkiaSharp;

namespace SignalChat.Api.Tests.Profile;

public sealed class AvatarImageProcessorTests
{
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 255, 0);
    private static readonly SKColor Blue = new(0, 0, 255);

    private readonly AvatarImageProcessor _processor = new();

    public static TheoryData<string, byte[]> AcceptedPictures => new()
    {
        { "png", TestImages.Png() },
        { "jpeg", TestImages.Jpeg() },
        { "gif", TestImages.Gif() },
        { "webp", TestImages.Webp() },
    };

    [Theory]
    [MemberData(nameof(AcceptedPictures))]
    public void AcceptedPicture_BecomesASquareWebp(string format, byte[] source)
    {
        var result = _processor.Process(source);

        using var codec = SKCodec.Create(new MemoryStream(result));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(AvatarLimits.OutputSidePx, codec.Info.Width);
        Assert.Equal(AvatarLimits.OutputSidePx, codec.Info.Height);
        Assert.False(string.IsNullOrEmpty(format));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("MZ this is not a picture")]
    [InlineData("")]
    public void NonImageContent_IsRefused(string content)
    {
        var error = Assert.Throws<ChatRuleException>(() => _processor.Process(Encoding.UTF8.GetBytes(content)));

        Assert.Equal(UserProfileErrorCodes.InvalidAvatar, error.Code);
    }

    [Fact]
    public void TruncatedPicture_IsRefused()
    {
        var png = TestImages.Bands(SKEncodedImageFormat.Png, 300, 200, Red, Green, Blue);

        var error = Assert.Throws<ChatRuleException>(() => _processor.Process(png[..(png.Length / 2)]));

        Assert.Equal(UserProfileErrorCodes.InvalidAvatar, error.Code);
    }

    [Fact]
    public void PictureWiderThanTheLimit_IsRefusedBeforeItIsDecoded()
    {
        var wide = TestImages.Png(AvatarLimits.MaxSourceSidePx + 1, 2);

        var error = Assert.Throws<ChatRuleException>(() => _processor.Process(wide));

        Assert.Equal(UserProfileErrorCodes.InvalidAvatar, error.Code);
    }

    [Fact]
    public void FileOverTheSizeLimit_IsRefused()
    {
        var huge = new byte[AvatarLimits.MaxUploadBytes + 1];

        var error = Assert.Throws<ChatRuleException>(() => _processor.Process(huge));

        Assert.Equal(UserProfileErrorCodes.AvatarTooLarge, error.Code);
    }

    [Fact]
    public void DataHiddenAfterThePicture_DoesNotSurviveReencoding()
    {
        const string payload = "<script>alert(document.cookie)</script>";
        var polyglot = TestImages.Png().Concat(Encoding.UTF8.GetBytes(payload)).ToArray();

        var result = _processor.Process(polyglot);

        Assert.DoesNotContain(payload, Encoding.Latin1.GetString(result), StringComparison.Ordinal);
    }

    [Fact]
    public void WidePicture_IsCroppedFromTheCenter_NotStretched()
    {
        // Blue, red and green thirds side by side. A centre crop keeps only the red middle; a picture
        // squeezed to a square would still show blue and green at its edges.
        var source = TestImages.Bands(SKEncodedImageFormat.Png, 400, 100, Blue, Red, Green);

        var result = _processor.Process(source);

        var middle = AvatarLimits.OutputSidePx / 2;
        foreach (var x in new[] { 8, middle, AvatarLimits.OutputSidePx - 8 })
        {
            AssertNearly(Red, PixelAt(result, x, middle));
        }
    }

    [Fact]
    public void ExifOrientation_TurnsThePictureUpright()
    {
        // Left half blue, right half red, flagged "turn 90 degrees clockwise" (6): the left half ends up on top.
        var sideways = TestImages.WithExifOrientation(
            TestImages.Bands(SKEncodedImageFormat.Jpeg, 100, 50, Blue, Red), orientation: 6);

        var result = _processor.Process(sideways);

        AssertNearly(Blue, PixelAt(result, AvatarLimits.OutputSidePx / 2, 10));
        AssertNearly(Red, PixelAt(result, AvatarLimits.OutputSidePx / 2, AvatarLimits.OutputSidePx - 10));
    }

    private static SKColor PixelAt(byte[] picture, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(picture);
        return bitmap.GetPixel(x, y);
    }

    private static void AssertNearly(SKColor expected, SKColor actual)
    {
        const int Tolerance = 60;
        Assert.True(
            Math.Abs(expected.Red - actual.Red) < Tolerance
            && Math.Abs(expected.Green - actual.Green) < Tolerance
            && Math.Abs(expected.Blue - actual.Blue) < Tolerance,
            $"expected about {expected}, got {actual}");
    }
}

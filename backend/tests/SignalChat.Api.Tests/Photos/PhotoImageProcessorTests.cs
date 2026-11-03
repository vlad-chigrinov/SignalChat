using System.Text;
using SignalChat.Api.Tests.Profile;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Media;
using SkiaSharp;

namespace SignalChat.Api.Tests.Photos;

public sealed class PhotoImageProcessorTests
{
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 255, 0);

    private readonly PhotoImageProcessor _processor = new();

    public static TheoryData<byte[]> SupportedUploads => new()
    {
        TestImages.Jpeg(),
        TestImages.Png(),
        TestImages.Webp(),
        TestImages.Gif(),
    };

    [Theory]
    [MemberData(nameof(SupportedUploads))]
    public async Task Process_AcceptsEveryFormatByItsContent_AndStoresWebp(byte[] upload)
    {
        var photo = await _processor.ProcessAsync(upload, CancellationToken.None);

        Assert.Equal(SKEncodedImageFormat.Webp, FormatOf(photo.Original));
        Assert.Equal(SKEncodedImageFormat.Webp, FormatOf(photo.Thumbnail));
    }

    [Fact]
    public async Task Process_ASmallPicture_KeepsItsSize()
    {
        var photo = await _processor.ProcessAsync(TestImages.Png(300, 200), CancellationToken.None);

        Assert.Equal((300, 200), (photo.Width, photo.Height));
        Assert.Equal((300, 200), SizeOf(photo.Original));
        Assert.Equal((300, 200), (photo.ThumbnailWidth, photo.ThumbnailHeight));
    }

    [Fact]
    public async Task Process_ALargePicture_IsScaledDownKeepingItsProportions_AndGetsAThumbnail()
    {
        var photo = await _processor.ProcessAsync(TestImages.Jpeg(4000, 3000), CancellationToken.None);

        Assert.Equal((PhotoLimits.StoredMaxSidePx, 1920), (photo.Width, photo.Height));
        Assert.Equal((photo.Width, photo.Height), SizeOf(photo.Original));
        Assert.Equal((PhotoLimits.ThumbnailMaxSidePx, 480), (photo.ThumbnailWidth, photo.ThumbnailHeight));
        Assert.Equal((photo.ThumbnailWidth, photo.ThumbnailHeight), SizeOf(photo.Thumbnail));
    }

    [Fact]
    public async Task Process_ATallPicture_IsBoundedOnItsLongerSide()
    {
        var photo = await _processor.ProcessAsync(TestImages.Png(1000, 3000), CancellationToken.None);

        Assert.Equal((853, PhotoLimits.StoredMaxSidePx), (photo.Width, photo.Height));
    }

    [Fact]
    public async Task Process_ExifOrientation_TurnsThePhotoUpright_AndSwapsItsSize()
    {
        // Stored as 300x200 with the "rotate 90 clockwise" tag (6): it is upright as 200x300, the left band on top.
        var jpeg = TestImages.WithExifOrientation(TestImages.Bands(SKEncodedImageFormat.Jpeg, 300, 200, Red, Green), orientation: 6);

        var photo = await _processor.ProcessAsync(jpeg, CancellationToken.None);

        Assert.Equal((200, 300), (photo.Width, photo.Height));
        using var bitmap = SKBitmap.Decode(photo.Original);
        AssertNear(Red, bitmap.GetPixel(100, 40));
        AssertNear(Green, bitmap.GetPixel(100, 260));
    }

    [Fact]
    public async Task Process_DropsMetadata_AndTrailingBytes()
    {
        var withLocation = AddComment(TestImages.Jpeg(), "GPSLatitude=53.9;Camera=Secret");
        var polyglot = new byte[][] { TestImages.Png(), "<script>alert(1)</script>PK"u8.ToArray() }.SelectMany(part => part).ToArray();

        var fromJpeg = await _processor.ProcessAsync(withLocation, CancellationToken.None);
        var fromPng = await _processor.ProcessAsync(polyglot, CancellationToken.None);

        foreach (var stored in new[] { fromJpeg.Original, fromJpeg.Thumbnail, fromPng.Original, fromPng.Thumbnail })
        {
            Assert.False(Contains(stored, "GPSLatitude"));
            Assert.False(Contains(stored, "Secret"));
            Assert.False(Contains(stored, "<script>"));
            Assert.False(Contains(stored, "Exif"));
        }
    }

    [Theory]
    [InlineData("plain text, not a picture")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("%PDF-1.7 fake")]
    public async Task Process_ANonImage_IsRefused_WhateverItIsCalled(string content)
    {
        var error = await Assert.ThrowsAsync<ChatRuleException>(
            () => _processor.ProcessAsync(Encoding.UTF8.GetBytes(content), CancellationToken.None));

        Assert.Equal(AttachmentErrorCodes.InvalidImage, error.Code);
    }

    [Fact]
    public async Task Process_AnEmptyUpload_IsRefused()
    {
        var error = await Assert.ThrowsAsync<ChatRuleException>(() => _processor.ProcessAsync(Array.Empty<byte>(), CancellationToken.None));

        Assert.Equal(AttachmentErrorCodes.InvalidImage, error.Code);
    }

    [Fact]
    public async Task Process_ADamagedPicture_IsRefused()
    {
        var png = TestImages.Png(400, 400);

        var error = await Assert.ThrowsAsync<ChatRuleException>(
            () => _processor.ProcessAsync(png[..(png.Length / 2)], CancellationToken.None));

        Assert.Equal(AttachmentErrorCodes.InvalidImage, error.Code);
    }

    [Theory]
    [InlineData(PhotoLimits.MaxSourceSidePx + 1, 10)]
    [InlineData(11000, 11000)]
    public async Task Process_AHugePictureHeader_IsRefusedWithoutDecoding(int width, int height)
    {
        var bomb = TestImages.PngHeaderOnly(width, height);

        var error = await Assert.ThrowsAsync<ChatRuleException>(() => _processor.ProcessAsync(bomb, CancellationToken.None));

        Assert.Equal(AttachmentErrorCodes.InvalidImage, error.Code);
    }

    private static SKEncodedImageFormat FormatOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(bytes));
        return codec.EncodedFormat;
    }

    private static (int Width, int Height) SizeOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(bytes));
        return (codec.Info.Width, codec.Info.Height);
    }

    private static void AssertNear(SKColor expected, SKColor actual)
    {
        const int Tolerance = 12;
        Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, Tolerance);
        Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, Tolerance);
        Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, Tolerance);
    }

    private static bool Contains(byte[] bytes, string text) => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    // A JPEG comment segment (COM, 0xFFFE) right after the start-of-image marker.
    private static byte[] AddComment(byte[] jpeg, string comment)
    {
        var text = Encoding.ASCII.GetBytes(comment);
        byte[] length = [(byte)((text.Length + 2) >> 8), (byte)((text.Length + 2) & 0xFF)];
        return [.. jpeg[..2], 0xFF, 0xFE, .. length, .. text, .. jpeg[2..]];
    }
}

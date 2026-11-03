using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Media;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Photos;

/// <summary>Sending photos with a caption: what is accepted, what is refused, and that a refusal leaves nothing behind.</summary>
public sealed class PhotoMessageSendTests : IDisposable
{
    private readonly PhotoApiFactory _factory = new();
    private readonly HttpClient _client;

    public PhotoMessageSendTests() => _client = _factory.CreateApiClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task WithoutASession_Returns401()
    {
        var response = await _client.SendAsync(PhotoHttp.Send(Guid.NewGuid(), cookie: null, "hi", UploadFile.Png()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AMember_SendsOnePhotoWithACaption_AndGetsTheMessageBack()
    {
        var (author, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-author"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);

        var message = await PhotoHttp.SendOkAsync(_client, roomId, cookie, "  Lake at dawn  ", UploadFile.Png(300, 200, "lake.png"));

        Assert.Equal("Lake at dawn", message.GetProperty("text").GetString());
        Assert.Equal(roomId, message.GetProperty("conversationId").GetGuid());
        Assert.Equal(author.Id, message.GetProperty("author").GetProperty("id").GetGuid());
        var photo = Assert.Single(message.GetProperty("attachments").EnumerateArray());
        Assert.Equal((300, 200), (photo.GetProperty("width").GetInt32(), photo.GetProperty("height").GetInt32()));
        Assert.Equal("lake.webp", photo.GetProperty("fileName").GetString());
        Assert.Equal("image/webp", photo.GetProperty("contentType").GetString());
        Assert.StartsWith("/api/attachments/", photo.GetProperty("url").GetString());
        Assert.EndsWith("/thumbnail", photo.GetProperty("thumbnailUrl").GetString());
        Assert.Equal(2, _factory.Store.Keys.Count);
    }

    [Fact]
    public async Task ThePhotoNeedsNoCaption()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-nocaption"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);

        var withoutField = await PhotoHttp.SendOkAsync(_client, roomId, cookie, text: null, UploadFile.Png());
        var blankField = await PhotoHttp.SendOkAsync(_client, roomId, cookie, text: "   ", UploadFile.Png());

        Assert.Equal(string.Empty, withoutField.GetProperty("text").GetString());
        Assert.Equal(string.Empty, blankField.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Message.MaxPhotos)]
    public async Task OneToNinePhotos_AreAccepted_InTheOrderSent(int count)
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile($"ph-count-{count}"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var files = Enumerable.Range(0, count).Select(index => UploadFile.Png(100 + index, 50, $"p{index}.png")).ToArray();

        var message = await PhotoHttp.SendOkAsync(_client, roomId, cookie, "set", files);

        var widths = message.GetProperty("attachments").EnumerateArray().Select(photo => photo.GetProperty("width").GetInt32());
        Assert.Equal(Enumerable.Range(0, count).Select(index => 100 + index), widths);
        Assert.Equal(count * 2, _factory.Store.Keys.Count);
    }

    [Fact]
    public async Task NoPhoto_Returns400_AndKeepsNothing()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-none"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "only text"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.InvalidAttachments, await PhotoHttp.ErrorCodeAsync(response));
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "only text"));
    }

    [Fact]
    public async Task TenPhotos_Return400_AndTheStoreStaysEmpty()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-ten"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var files = Enumerable.Range(0, Message.MaxPhotos + 1).Select(index => UploadFile.Png(40, 40, $"p{index}.png")).ToArray();

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "too many", files));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.InvalidAttachments, await PhotoHttp.ErrorCodeAsync(response));
        Assert.Empty(_factory.Store.Keys);
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "too many"));
    }

    [Fact]
    public async Task ARequestThatIsNotMultipart_Returns400()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-json"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);

        var response = await _client.SendAsync(ChatHttp.Post(PhotoHttp.SendPath(roomId), cookie, new { text = "no files" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.InvalidAttachments, await PhotoHttp.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task ACaptionOverTheLimit_Returns400_AndKeepsNothing()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-long"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, new string('x', Message.MaxTextLength + 1), UploadFile.Png()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_message", await PhotoHttp.ErrorCodeAsync(response));
        Assert.Empty(_factory.Store.Keys);
    }

    [Theory]
    [InlineData("holiday.jpg", "image/jpeg")]
    [InlineData("notes.txt", "text/plain")]
    public async Task TheTypeIsDecidedByTheContent_NotByTheNameOrMediaType(string fileName, string contentType)
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-forged"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var script = new UploadFile(fileName, contentType, "<script>alert(1)</script>"u8.ToArray());
        var realPicture = new UploadFile(fileName, contentType, Profile.TestImages.Png(50, 50));

        var refused = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, null, script));
        var accepted = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, null, realPicture));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(AttachmentErrorCodes.InvalidImage, await PhotoHttp.ErrorCodeAsync(refused));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task OneBadFileAmongGoodOnes_RefusesTheWholeMessage_AndRemovesTheStoredOnes()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-mixed"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var notAPicture = new UploadFile("x.png", "image/png", "not an image"u8.ToArray());

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "mixed", UploadFile.Png(), UploadFile.Png(), notAPicture));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Store.Keys);
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "mixed"));
    }

    [Fact]
    public async Task AFileOverTheLimit_Returns413()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-big"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var tooBig = new UploadFile("big.png", "image/png", new byte[PhotoLimits.MaxFileBytes + 1]);

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, null, tooBig));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.AttachmentTooLarge, await PhotoHttp.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task FilesThatTogetherAreOverTheLimit_Return413_AndKeepNothing()
    {
        _factory.Processor.Stub = true;
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-total"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var eachBytes = PhotoLimits.MaxFileBytes - 1;
        var files = Enumerable.Range(0, PhotoLimits.MaxTotalBytes / eachBytes + 1)
            .Select(index => new UploadFile($"p{index}.png", "image/png", new byte[eachBytes])).ToArray();

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "heavy", files));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.AttachmentTooLarge, await PhotoHttp.ErrorCodeAsync(response));
        Assert.Empty(_factory.Store.Keys);
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "heavy"));
    }

    [Fact]
    public async Task APictureWithHugeDimensions_IsRefused_WithoutBeingDecoded()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-bomb"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var bomb = new UploadFile("bomb.png", "image/png", Profile.TestImages.PngHeaderOnly(30000, 30000));

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, null, bomb));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.InvalidImage, await PhotoHttp.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task WhenTheStoreFailsOnTheSecondFile_NothingIsLeftBehind()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-storefail"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        // Put 1 and 2 are the first photo (picture, thumbnail); put 3 is the second photo's picture.
        _factory.Storage.FailPutNumber = 3;

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "doomed", UploadFile.Png(), UploadFile.Png()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(AttachmentErrorCodes.StorageUnavailable, await PhotoHttp.ErrorCodeAsync(response));
        Assert.Empty(_factory.Store.Keys);
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "doomed"));
    }

    [Fact]
    public async Task WhenTheDatabaseRefusesTheMessage_TheStoredFilesAreRemoved()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ph-dbfail"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        await DropAttachmentsTableAsync();

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, cookie, "no table", UploadFile.Png()));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.Store.Keys);
    }

    [Fact]
    public async Task APhotoMessage_IsLimitedPerMinute()
    {
        using var limited = new LimitedPhotoFactory();
        var (_, cookie) = await limited.CreateSessionAsync(HubFixtures.Profile("ph-limit"));
        using var client = limited.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Infrastructure.LowRateLimitApiFactory.TightRestPermitsPerMinute + 1; attempt++)
        {
            statuses.Add((await client.SendAsync(PhotoHttp.Send(roomId, cookie, null, UploadFile.Png(20, 20)))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses[..^1], status => Assert.Equal(HttpStatusCode.Created, status));
    }

    private async Task DropAttachmentsTableAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>().Database;
        await database.ExecuteSqlRawAsync("DROP TABLE message_attachments");
    }

    private sealed class LimitedPhotoFactory : PhotoApiFactory
    {
        protected override int RestPermitsPerMinute => Infrastructure.LowRateLimitApiFactory.TightRestPermitsPerMinute;
    }
}

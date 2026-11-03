using System.Net;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Profile;
using SkiaSharp;

namespace SignalChat.Api.Tests.Photos;

/// <summary>A photo is served by the API to the people of its conversation only; every other caller gets the same 404.</summary>
public sealed class PhotoAccessTests : IDisposable
{
    private readonly PhotoApiFactory _factory = new();
    private readonly HttpClient _client;

    public PhotoAccessTests() => _client = _factory.CreateApiClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ARoomMember_ReadsThePictureAndTheThumbnail()
    {
        var (_, ownerCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-owner"));
        var (_, mateCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-mate"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, ownerCookie);
        await HubFixtures.JoinRoomAsync(_client, mateCookie, roomId);
        var photo = await SendOnePhotoAsync(roomId, ownerCookie, UploadFile.Png(900, 600));

        var original = await _client.SendAsync(ChatHttp.Get(photo.GetProperty("url").GetString()!, mateCookie));
        var thumbnail = await _client.SendAsync(ChatHttp.Get(photo.GetProperty("thumbnailUrl").GetString()!, mateCookie));

        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("image/webp", original.Content.Headers.ContentType?.MediaType);
        Assert.Equal((900, 600), SizeOf(await original.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal(
            (photo.GetProperty("thumbnailWidth").GetInt32(), photo.GetProperty("thumbnailHeight").GetInt32()),
            SizeOf(await thumbnail.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task APicture_IsCachedPrivately_AndNeverSniffed()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-cache"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var photo = await SendOnePhotoAsync(roomId, cookie, UploadFile.Png());

        var response = await _client.SendAsync(ChatHttp.Get(photo.GetProperty("url").GetString()!, cookie));

        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.MaxAge > TimeSpan.FromDays(1));
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task Download_SendsTheOriginalAsAnAttachment_UnderASafeName()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-download"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var hostile = UploadFile.Png(50, 50, name: "..\\..\\evil/ my:photo?.PNG");
        var photo = await SendOnePhotoAsync(roomId, cookie, hostile);

        var response = await _client.SendAsync(ChatHttp.Get(photo.GetProperty("url").GetString()! + "?download=true", cookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("my_photo_.webp", response.Content.Headers.ContentDisposition?.FileName?.Trim((char)34));
    }

    [Fact]
    public async Task ANonMember_AndAVisitorWithoutASession_CannotReadIt()
    {
        var (_, ownerCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-secret-owner"));
        var (_, outsiderCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-outsider"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, ownerCookie);
        var photo = await SendOnePhotoAsync(roomId, ownerCookie, UploadFile.Png());
        var url = photo.GetProperty("url").GetString()!;
        var thumbnailUrl = photo.GetProperty("thumbnailUrl").GetString()!;

        var outsider = await _client.SendAsync(ChatHttp.Get(url, outsiderCookie));
        var outsiderThumbnail = await _client.SendAsync(ChatHttp.Get(thumbnailUrl, outsiderCookie));
        var visitor = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderThumbnail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitor.StatusCode);
    }

    [Fact]
    public async Task AMemberWhoLeftTheRoom_CannotReadItAnyMore()
    {
        var (_, ownerCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-leave-owner"));
        var (_, leaverCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-leaver"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, ownerCookie);
        await HubFixtures.JoinRoomAsync(_client, leaverCookie, roomId);
        var photo = await SendOnePhotoAsync(roomId, ownerCookie, UploadFile.Png());
        var url = photo.GetProperty("url").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(ChatHttp.Get(url, leaverCookie))).StatusCode);

        await HubFixtures.LeaveRoomOverRestAsync(_client, leaverCookie, roomId);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(ChatHttp.Get(url, leaverCookie))).StatusCode);
    }

    [Fact]
    public async Task AnUnknownPicture_AndAnUnknownVariant_Return404()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-unknown"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var photo = await SendOnePhotoAsync(roomId, cookie, UploadFile.Png());
        var id = photo.GetProperty("id").GetGuid();

        var missing = await _client.SendAsync(ChatHttp.Get($"/api/attachments/{Guid.NewGuid()}/original", cookie));
        var variant = await _client.SendAsync(ChatHttp.Get($"/api/attachments/{id}/huge", cookie));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, variant.StatusCode);
    }

    [Fact]
    public async Task WhenTheStoreIsDown_ThePictureAnswers503()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-down"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        var photo = await SendOnePhotoAsync(roomId, cookie, UploadFile.Png());
        _factory.Storage.FailOpen = true;

        var response = await _client.SendAsync(ChatHttp.Get(photo.GetProperty("url").GetString()!, cookie));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task InAnAcceptedDirectConversation_BothPeopleSendAndReadPhotos()
    {
        var (alice, aliceCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-alice"));
        var (bob, bobCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-bob"));
        var (_, carolCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pa-carol"));
        var conversationId = await DirectRequestFixtures.RequestAsync(_client, aliceCookie, bob.Id, "hello");
        await DirectRequestFixtures.AcceptAsync(_client, bobCookie, conversationId);

        var fromAlice = await SendOnePhotoAsync(conversationId, aliceCookie, UploadFile.Png());
        var fromBob = await SendOnePhotoAsync(conversationId, bobCookie, UploadFile.Png());

        foreach (var photo in new[] { fromAlice, fromBob })
        {
            var url = photo.GetProperty("url").GetString()!;
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(ChatHttp.Get(url, aliceCookie))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(ChatHttp.Get(url, bobCookie))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(ChatHttp.Get(url, carolCookie))).StatusCode);
        }
        Assert.NotEqual(alice.Id, bob.Id);
    }

    [Fact]
    public async Task InAPendingDirectRequest_NeitherTheInitiatorNorTheRecipientCanSendPhotos()
    {
        var (_, aliceCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pp-alice"));
        var (bob, bobCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pp-bob"));
        var conversationId = await DirectRequestFixtures.RequestAsync(_client, aliceCookie, bob.Id, "may I?");

        var fromInitiator = await _client.SendAsync(PhotoHttp.Send(conversationId, aliceCookie, "sneaky", UploadFile.Png()));
        var fromRecipient = await _client.SendAsync(PhotoHttp.Send(conversationId, bobCookie, "early", UploadFile.Png()));

        Assert.Equal(HttpStatusCode.Conflict, fromInitiator.StatusCode);
        Assert.Equal("dm_not_accepted", await PhotoHttp.ErrorCodeAsync(fromInitiator));
        Assert.Equal(HttpStatusCode.NotFound, fromRecipient.StatusCode);
        Assert.Empty(_factory.Store.Keys);
    }

    [Fact]
    public async Task ANonMember_CannotSendPhotos_AndNothingIsStored()
    {
        var (_, ownerCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ps-owner"));
        var (_, outsiderCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("ps-outsider"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, ownerCookie);

        var response = await _client.SendAsync(PhotoHttp.Send(roomId, outsiderCookie, "intruder", UploadFile.Png()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_factory.Store.Keys);
        Assert.Equal(0, await HubFixtures.StoredCountAsync(_factory, "intruder"));
    }

    private async Task<System.Text.Json.JsonElement> SendOnePhotoAsync(Guid conversationId, string cookie, UploadFile file)
    {
        var message = await PhotoHttp.SendOkAsync(_client, conversationId, cookie, null, file);
        return message.GetProperty("attachments")[0];
    }

    private static (int Width, int Height) SizeOf(byte[] bytes)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(bytes));
        return (codec.Info.Width, codec.Info.Height);
    }
}

using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Hubs;

namespace SignalChat.Api.Tests.Photos;

/// <summary>A photo message reaches the conversation live, shows up in history, and previews in the lists.</summary>
public sealed class PhotoDeliveryTests : IDisposable
{
    private const string EventName = "MessageReceived";

    private readonly PhotoApiFactory _factory = new();
    private readonly HttpClient _client;

    public PhotoDeliveryTests() => _client = _factory.CreateApiClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task TheRoom_HearsTheMessageWithItsPhotos_AndAnOutsiderHearsNothing()
    {
        var (_, ownerCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-owner"));
        var (_, mateCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-mate"));
        var (_, outsiderCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-outsider"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, ownerCookie);
        await HubFixtures.JoinRoomAsync(_client, mateCookie, roomId);
        await using var mate = await HubTestClient.ConnectAsync(_factory, mateCookie);
        await using var outsider = await HubTestClient.ConnectAsync(_factory, outsiderCookie);
        var mateInbox = new HubEventInbox<MessageDto>(mate, EventName);
        var outsiderInbox = new HubEventInbox<MessageDto>(outsider, EventName);
        await mate.InvokeAsync("JoinRoom", roomId.ToString());

        var sent = await PhotoHttp.SendOkAsync(_client, roomId, ownerCookie, "look", UploadFile.Png(), UploadFile.Png(60, 90));

        var heard = await mateInbox.NextAsync();
        Assert.Equal(sent.GetProperty("id").GetGuid(), heard.Id);
        Assert.Equal("look", heard.Text);
        Assert.Equal(2, heard.Attachments.Count);
        Assert.Equal((60, 90), (heard.Attachments[1].Width, heard.Attachments[1].Height));
        await outsiderInbox.AssertSilenceAsync();
    }

    [Fact]
    public async Task ThePeerOfADirectConversation_HearsThePhoto()
    {
        var (_, aliceCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-alice"));
        var (bob, bobCookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-bob"));
        var conversationId = await DirectRequestFixtures.RequestAsync(_client, aliceCookie, bob.Id, "hi");
        await DirectRequestFixtures.AcceptAsync(_client, bobCookie, conversationId);
        await using var bobHub = await HubTestClient.ConnectAsync(_factory, bobCookie);
        var inbox = new HubEventInbox<MessageDto>(bobHub, EventName);
        await bobHub.InvokeAsync("JoinRoom", conversationId.ToString());

        await PhotoHttp.SendOkAsync(_client, conversationId, aliceCookie, null, UploadFile.Png());

        var heard = await inbox.NextAsync();
        Assert.Single(heard.Attachments);
    }

    [Fact]
    public async Task History_ReturnsThePhotosInOrder_AndTextMessagesCarryNone()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-history"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        await using var hub = await HubTestClient.ConnectAsync(_factory, cookie);
        await hub.InvokeAsync("JoinRoom", roomId.ToString());
        await hub.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "plain words");
        await PhotoHttp.SendOkAsync(_client, roomId, cookie, "pictures", UploadFile.Png(10, 20), UploadFile.Png(30, 40), UploadFile.Png(50, 60));

        var page = await ChatHttp.ReadJsonAsync(await _client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages", cookie)));

        var items = page.GetProperty("items").EnumerateArray().ToList();
        // The test clock is frozen, so the two messages share a time and their order is not what is under test here.
        var text = items.Single(item => item.GetProperty("text").GetString() == "plain words");
        var photos = items.Single(item => item.GetProperty("text").GetString() == "pictures");
        Assert.Empty(text.GetProperty("attachments").EnumerateArray());
        var widths = photos.GetProperty("attachments").EnumerateArray().Select(photo => photo.GetProperty("width").GetInt32());
        Assert.Equal([10, 30, 50], widths);
    }

    [Fact]
    public async Task APhotoOnlyMessage_ShowsAsAPhotoCountInTheRoomListPreview()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-preview"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        await PhotoHttp.SendOkAsync(_client, roomId, cookie, null, UploadFile.Png(), UploadFile.Png());

        var rooms = await ChatHttp.ReadJsonAsync(await _client.SendAsync(ChatHttp.Get("/api/rooms", cookie)));

        var room = rooms.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == roomId);
        var last = room.GetProperty("lastMessage");
        Assert.Equal(string.Empty, last.GetProperty("text").GetString());
        Assert.Equal(2, last.GetProperty("photoCount").GetInt32());
    }

    [Fact]
    public async Task ThePlainTextHubPath_StillRefusesAnEmptyMessage()
    {
        var (_, cookie) = await _factory.CreateSessionAsync(HubFixtures.Profile("pd-textonly"));
        var roomId = await HubFixtures.CreateRoomAsync(_client, cookie);
        await using var hub = await HubTestClient.ConnectAsync(_factory, cookie);
        await hub.InvokeAsync("JoinRoom", roomId.ToString());

        var error = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            hub.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "   "));

        Assert.Equal("invalid_message", HubTestErrors.CodeOf(error));
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages", cookie))).StatusCode);
    }
}

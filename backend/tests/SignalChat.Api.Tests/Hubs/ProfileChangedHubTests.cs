using System.Net.Http.Json;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Api.Tests.Profile;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>A profile change reaches the people who see the user in a room or a chat, and the user's other tabs.</summary>
public sealed class ProfileChangedHubTests
{
    private const string EventName = "UserProfileChanged";

    [Fact]
    public async Task RoomMate_IsToldTheNewName()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-owner"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-mate"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        await using var mateHub = await HubTestClient.ConnectAsync(factory, mateCookie);
        var changes = new HubEventInbox<UserSummaryDto>(mateHub, EventName);

        var response = await client.SendAsync(ChatHttp.Put("/api/me/display-name", ownerCookie, new { displayName = "Renamed Owner" }));
        response.EnsureSuccessStatusCode();
        var change = await changes.NextAsync();

        Assert.Equal(owner.Id, change.Id);
        Assert.Equal("Renamed Owner", change.DisplayName);
    }

    [Fact]
    public async Task RoomMate_IsToldTheNewAvatar()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pa-owner"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pa-mate"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        await using var mateHub = await HubTestClient.ConnectAsync(factory, mateCookie);
        var changes = new HubEventInbox<UserSummaryDto>(mateHub, EventName);

        var request = ChatHttp.Put("/api/me/avatar", ownerCookie, null);
        request.Content = new ByteArrayContent(TestImages.Png());
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
        var change = await changes.NextAsync();

        Assert.Equal($"/api/users/{owner.Id}/avatar?v=1", change.AvatarUrl);
    }

    [Fact]
    public async Task TheUsersOtherTab_IsToldToo()
    {
        using var factory = new SignalChatApiFactory();
        var (user, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-self"));
        using var client = factory.CreateApiClient();
        await using var otherTab = await HubTestClient.ConnectAsync(factory, cookie);
        var changes = new HubEventInbox<UserSummaryDto>(otherTab, EventName);

        (await client.SendAsync(ChatHttp.Put("/api/me/display-name", cookie, new { displayName = "Same Person" }))).EnsureSuccessStatusCode();
        var change = await changes.NextAsync();

        Assert.Equal(user.Id, change.Id);
        Assert.Equal("Same Person", change.DisplayName);
    }

    [Fact]
    public async Task AUserWithoutASharedConversation_IsNotTold()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-alone"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-stranger"));
        using var client = factory.CreateApiClient();
        await using var strangerHub = await HubTestClient.ConnectAsync(factory, strangerCookie);
        var changes = new HubEventInbox<UserSummaryDto>(strangerHub, EventName);

        (await client.SendAsync(ChatHttp.Put("/api/me/display-name", cookie, new { displayName = "Nobody Cares" }))).EnsureSuccessStatusCode();

        await changes.AssertSilenceAsync();
    }

    [Fact]
    public async Task ARefusedChange_SendsNothing()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pc-refused"));
        using var client = factory.CreateApiClient();
        await using var ownTab = await HubTestClient.ConnectAsync(factory, cookie);
        var changes = new HubEventInbox<UserSummaryDto>(ownTab, EventName);

        var response = await client.SendAsync(ChatHttp.Put("/api/me/display-name", cookie, new { displayName = "   " }));

        Assert.False(response.IsSuccessStatusCode);
        await changes.AssertSilenceAsync();
    }
}

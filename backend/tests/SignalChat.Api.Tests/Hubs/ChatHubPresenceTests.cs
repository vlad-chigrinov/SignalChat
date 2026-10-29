using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Presence goes only to users who share a conversation, and counts connections per user.</summary>
public sealed class ChatHubPresenceTests
{
    [Fact]
    public async Task PeerSeesOnlineAndOffline_OnlyAfterTheLastConnectionCloses()
    {
        using var factory = new SignalChatApiFactory();
        var (peer, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pre-peer"));
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("pre-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        var presence = new HubEventInbox<PresenceChangedDto>(peerHub, "PresenceChanged");

        var firstTab = await HubTestClient.ConnectAsync(factory, ownerCookie);
        var online = await presence.NextAsync();
        var secondTab = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await presence.AssertSilenceAsync();
        await firstTab.DisposeAsync();
        await presence.AssertSilenceAsync();
        await secondTab.DisposeAsync();
        var announcer = factory.Services.GetRequiredService<PresenceAnnouncer>();
        await Eventually.UntilAsync(() => announcer.HasPendingOffline(owner.Id), "the offline grace period to start");
        factory.Clock.Advance(ChatHubLimits.PresenceOfflineGrace);
        var offline = await presence.NextAsync();

        Assert.Equal(owner.Id, online.UserId);
        Assert.True(online.IsOnline);
        Assert.Equal(owner.Id, offline.UserId);
        Assert.False(offline.IsOnline);
        Assert.NotEqual(peer.Id, owner.Id);
    }

    [Fact]
    public async Task UserWithoutASharedConversation_ReceivesNoPresence()
    {
        using var factory = new SignalChatApiFactory();
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("iso-peer"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("iso-stranger"));
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        var presence = new HubEventInbox<PresenceChangedDto>(peerHub, "PresenceChanged");

        await using var strangerHub = await HubTestClient.ConnectAsync(factory, strangerCookie);

        await presence.AssertSilenceAsync();
    }

    [Fact]
    public async Task OnlinePeersEndpoint_ListsOnlyOnlinePeersWithASharedConversation()
    {
        using var factory = new SignalChatApiFactory();
        var (_, callerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ep-caller"));
        var (onlinePeer, onlineCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ep-online"));
        var (_, offlineCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ep-offline"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, callerCookie);
        await HubFixtures.JoinRoomAsync(client, onlineCookie, roomId);
        await HubFixtures.JoinRoomAsync(client, offlineCookie, roomId);
        await using var onlineHub = await HubTestClient.ConnectAsync(factory, onlineCookie);
        await using var strangerHub = await HubTestClient.ConnectAsync(factory, await OutsideOnlineAsync(factory));

        var response = await client.SendAsync(ChatHttp.Get("/api/presence/online", callerCookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await ChatHttp.ReadJsonAsync(response)).EnumerateArray().Select(item => item.GetGuid()).ToList();
        Assert.Equal(new[] { onlinePeer.Id }, ids);
    }

    /// <summary>Puts a user online who shares nothing with the caller, so the endpoint must leave them out.</summary>
    private static async Task<string> OutsideOnlineAsync(SignalChatApiFactory factory)
    {
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ep-outsider"));
        return cookie;
    }
}

using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>"Offline" is announced only after the user's last connection has been gone for the whole grace period.</summary>
public sealed class ChatHubPresenceGraceTests
{
    /// <summary>How far into the grace period the user reconnects. Anything below the grace period works.</summary>
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ReconnectWithinTheGrace_AnnouncesNeitherOfflineNorOnlineAgain()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("grace-flicker-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("grace-flicker-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        var presence = new HubEventInbox<PresenceChangedDto>(peerHub, "PresenceChanged");
        var announcer = factory.Services.GetRequiredService<PresenceAnnouncer>();

        var firstTab = await HubTestClient.ConnectAsync(factory, ownerCookie);
        Assert.True((await presence.NextAsync()).IsOnline);
        await firstTab.DisposeAsync();
        await Eventually.UntilAsync(() => announcer.HasPendingOffline(owner.Id), "the offline grace period to start");

        factory.Clock.Advance(ReconnectDelay);
        await using var secondTab = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await presence.AssertSilenceAsync();

        // The grace timer was cancelled on reconnect, so moving past its original due time changes nothing.
        factory.Clock.Advance(ChatHubLimits.PresenceOfflineGrace);
        await presence.AssertSilenceAsync();
        Assert.True(announcer.IsOnline(owner.Id));
    }

    [Fact]
    public async Task NoReconnectWithinTheGrace_AnnouncesOfflineOnlyWhenTheGraceEnds()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("grace-offline-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("grace-offline-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        var presence = new HubEventInbox<PresenceChangedDto>(peerHub, "PresenceChanged");
        var announcer = factory.Services.GetRequiredService<PresenceAnnouncer>();

        var ownerTab = await HubTestClient.ConnectAsync(factory, ownerCookie);
        Assert.True((await presence.NextAsync()).IsOnline);
        await ownerTab.DisposeAsync();
        await Eventually.UntilAsync(() => announcer.HasPendingOffline(owner.Id), "the offline grace period to start");

        await presence.AssertSilenceAsync();
        Assert.True(announcer.IsOnline(owner.Id));

        factory.Clock.Advance(ChatHubLimits.PresenceOfflineGrace);
        var offline = await presence.NextAsync();

        Assert.Equal(owner.Id, offline.UserId);
        Assert.False(offline.IsOnline);
        Assert.False(announcer.IsOnline(owner.Id));
    }
}

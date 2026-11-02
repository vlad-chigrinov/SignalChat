using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Someone joins a room, on their own or by invitation: the room's open views, the invitee and presence are told live.</summary>
public sealed class RoomMembershipHubTests
{
    private const string MemberJoinedEvent = "RoomMemberJoined";
    private const string AddedToRoomEvent = "AddedToRoom";
    private const string PresenceEvent = "PresenceChanged";

    [Fact]
    public async Task MemberWithTheRoomOpen_IsToldWhenSomeoneJoinsOnTheirOwn()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rj-owner"));
        var (newcomer, newcomerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rj-newcomer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        var joined = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);

        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);

        var notice = await joined.NextAsync();
        Assert.Equal(roomId, notice.RoomId);
        Assert.Equal(newcomer.Id, notice.User.Id);
        Assert.Equal(newcomer.DisplayName, notice.User.DisplayName);
        Assert.NotEqual(default, notice.JoinedAt);
    }

    [Fact]
    public async Task JoiningAgain_SendsNothing()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rja-owner"));
        var (_, newcomerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rja-newcomer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        var joined = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);
        var presence = new HubEventInbox<PresenceChangedDto>(ownerHub, PresenceEvent);

        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);

        await joined.AssertSilenceAsync();
        await presence.AssertSilenceAsync();
    }

    [Fact]
    public async Task MemberWhoDidNotOpenTheRoom_GetsNoNotice_ButLearnsTheNewcomersPresence()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjn-owner"));
        var (newcomer, newcomerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjn-newcomer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var newcomerHub = await HubTestClient.ConnectAsync(factory, newcomerCookie);
        var joined = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);
        var presence = new HubEventInbox<PresenceChangedDto>(ownerHub, PresenceEvent);

        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);

        var change = await presence.NextAsync();
        Assert.Equal(newcomer.Id, change.UserId);
        Assert.True(change.IsOnline);
        await joined.AssertSilenceAsync();
    }

    [Fact]
    public async Task OutsiderWithAnOpenConnection_GetsNothing()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjo-owner"));
        var (_, newcomerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjo-newcomer"));
        var (_, outsiderCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjo-outsider"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var outsiderHub = await HubTestClient.ConnectAsync(factory, outsiderCookie);
        var joined = new HubEventInbox<RoomMemberJoinedDto>(outsiderHub, MemberJoinedEvent);
        var presence = new HubEventInbox<PresenceChangedDto>(outsiderHub, PresenceEvent);

        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);

        await joined.AssertSilenceAsync();
        await presence.AssertSilenceAsync();
    }

    [Fact]
    public async Task NewMember_GoingOffline_IsAnnouncedToTheOldMembers()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjp-owner"));
        var (newcomer, newcomerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rjp-newcomer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        var presence = new HubEventInbox<PresenceChangedDto>(ownerHub, PresenceEvent);
        var newcomerHub = await HubTestClient.ConnectAsync(factory, newcomerCookie);
        await HubFixtures.JoinRoomAsync(client, newcomerCookie, roomId);
        Assert.True((await presence.NextAsync()).IsOnline);

        await newcomerHub.DisposeAsync();
        var announcer = factory.Services.GetRequiredService<PresenceAnnouncer>();
        await Eventually.UntilAsync(() => announcer.HasPendingOffline(newcomer.Id), "the offline grace period to start");
        factory.Clock.Advance(ChatHubLimits.PresenceOfflineGrace);

        var offline = await presence.NextAsync();
        Assert.Equal(newcomer.Id, offline.UserId);
        Assert.False(offline.IsOnline);
    }

    [Fact]
    public async Task Invitee_IsToldLive_WithTheRoom_OnEveryTab()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ri-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ri-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await using var firstTab = await HubTestClient.ConnectAsync(factory, friendCookie);
        await using var secondTab = await HubTestClient.ConnectAsync(factory, friendCookie);
        var first = new HubEventInbox<AddedToRoomDto>(firstTab, AddedToRoomEvent);
        var second = new HubEventInbox<AddedToRoomDto>(secondTab, AddedToRoomEvent);

        await Invite(client, ownerCookie, roomId, friend.Id);

        foreach (var added in new[] { await first.NextAsync(), await second.NextAsync() })
        {
            Assert.Equal(roomId, added.Room.Id);
            Assert.Equal("room", added.Room.Kind);
            Assert.Equal(2, added.Room.MemberCount);
        }
    }

    [Fact]
    public async Task Invitee_CanOpenTheRoomRightAfterTheEvent_AndReceivesItsMessages()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rim-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rim-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var friendHub = await HubTestClient.ConnectAsync(factory, friendCookie);
        var added = new HubEventInbox<AddedToRoomDto>(friendHub, AddedToRoomEvent);
        var received = new HubEventInbox<MessageDto>(friendHub, "MessageReceived");
        var beforeInvite = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(
            () => friendHub.InvokeAsync("JoinRoom", roomId.ToString()));

        await Invite(client, ownerCookie, roomId, friend.Id);
        await added.NextAsync();
        await friendHub.InvokeAsync("JoinRoom", roomId.ToString());
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await ownerHub.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "welcome");

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(beforeInvite));
        Assert.Equal("welcome", (await received.NextAsync()).Text);
    }

    [Fact]
    public async Task OpenRoom_ShowsTheInviteeAsJoined_ToTheInviterToo()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rio-owner"));
        var (mate, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rio-mate"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rio-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var mateHub = await HubTestClient.ConnectAsync(factory, mateCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await mateHub.InvokeAsync("JoinRoom", roomId.ToString());
        var forInviter = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);
        var forMate = new HubEventInbox<RoomMemberJoinedDto>(mateHub, MemberJoinedEvent);

        await Invite(client, ownerCookie, roomId, friend.Id);

        Assert.Equal(friend.Id, (await forInviter.NextAsync()).User.Id);
        Assert.Equal(friend.Id, (await forMate.NextAsync()).User.Id);
        Assert.NotEqual(mate.Id, friend.Id);
    }

    [Fact]
    public async Task RefusedInvitation_SendsNoEvent()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rir-owner"));
        var (stranger, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rir-stranger"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await using var strangerHub = await HubTestClient.ConnectAsync(factory, strangerCookie);
        var joined = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);
        var added = new HubEventInbox<AddedToRoomDto>(strangerHub, AddedToRoomEvent);

        var response = await Invite(client, ownerCookie, roomId, stranger.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await joined.AssertSilenceAsync();
        await added.AssertSilenceAsync();
    }

    [Fact]
    public async Task InviteeAlreadyInTheRoom_GetsNoSecondEvent()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rid-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rid-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await Invite(client, ownerCookie, roomId, friend.Id);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await using var friendHub = await HubTestClient.ConnectAsync(factory, friendCookie);
        var joined = new HubEventInbox<RoomMemberJoinedDto>(ownerHub, MemberJoinedEvent);
        var added = new HubEventInbox<AddedToRoomDto>(friendHub, AddedToRoomEvent);

        var response = await Invite(client, ownerCookie, roomId, friend.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await joined.AssertSilenceAsync();
        await added.AssertSilenceAsync();
    }

    private static Task<HttpResponseMessage> Invite(HttpClient client, string cookie, Guid roomId, Guid userId) =>
        client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/invitations", cookie, new { userId }));
}

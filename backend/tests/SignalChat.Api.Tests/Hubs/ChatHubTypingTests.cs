using Microsoft.AspNetCore.SignalR.Client;
using HubException = Microsoft.AspNetCore.SignalR.HubException;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Typing hints: only joined members, never echoed to the sender, throttled per user and conversation.</summary>
public sealed class ChatHubTypingTests
{
    [Fact]
    public async Task Typing_ReachesOtherMembers_NotTheSender_AndIsThrottled()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("typ-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("typ-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await peerHub.InvokeAsync("JoinRoom", roomId.ToString());
        var peerTyping = new HubEventInbox<UserTypingEventDto>(peerHub, "UserTyping");
        var ownerTyping = new HubEventInbox<UserTypingEventDto>(ownerHub, "UserTyping");

        await ownerHub.InvokeAsync("Typing", roomId.ToString());
        await ownerHub.InvokeAsync("Typing", roomId.ToString());

        var hint = await peerTyping.NextAsync();
        Assert.Equal(roomId, hint.ConversationId);
        Assert.Equal(owner.Id, hint.UserId);
        await peerTyping.AssertSilenceAsync();
        await ownerTyping.AssertSilenceAsync();
    }

    [Fact]
    public async Task Typing_WithoutJoining_IsNotFound()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("typ-nojoin"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);

        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Typing", roomId.ToString()));

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(error));
    }

    [Fact]
    public async Task Typing_IsThrottled_PerUserAndConversation()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("thr-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("thr-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var ownerHub = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var peerHub = await HubTestClient.ConnectAsync(factory, peerCookie);
        await ownerHub.InvokeAsync("JoinRoom", roomId.ToString());
        await peerHub.InvokeAsync("JoinRoom", roomId.ToString());
        var peerTyping = new HubEventInbox<UserTypingEventDto>(peerHub, "UserTyping");

        await ownerHub.InvokeAsync("Typing", roomId.ToString());
        await peerTyping.NextAsync();
        factory.Clock.Advance(ChatHubLimits.TypingThrottle);
        await ownerHub.InvokeAsync("Typing", roomId.ToString());

        await peerTyping.NextAsync();
    }
}

using Microsoft.AspNetCore.SignalR.Client;
using HubException = Microsoft.AspNetCore.SignalR.HubException;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Two-client tests of the send path: delivery, membership, validation, rate limit and storage order.</summary>
public sealed class ChatHubMessagingTests
{
    [Fact]
    public async Task Members_ReceiveTheMessage_AfterItIsStored_AndTheSenderToo()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("msg-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("msg-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var owner = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var peer = await HubTestClient.ConnectAsync(factory, peerCookie);
        var ownerInbox = new HubEventInbox<MessageDto>(owner, "MessageReceived");
        var peerInbox = new HubEventInbox<MessageDto>(peer, "MessageReceived");
        await owner.InvokeAsync("JoinRoom", roomId.ToString());
        await peer.InvokeAsync("JoinRoom", roomId.ToString());

        var ack = await owner.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "hello room");

        var received = await peerInbox.NextAsync();
        Assert.Equal(ack.Id, received.Id);
        Assert.Equal("hello room", received.Text);
        Assert.Equal(1, await HubFixtures.StoredCountAsync(factory, "hello room"));
        Assert.Equal(ack.Id, (await ownerInbox.NextAsync()).Id);
    }

    [Fact]
    public async Task NonMember_CannotJoinOrSend_AndNobodyHearsAnything()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("nm-owner"));
        var (_, outsiderCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("nm-outsider"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await using var owner = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var outsider = await HubTestClient.ConnectAsync(factory, outsiderCookie);
        await owner.InvokeAsync("JoinRoom", roomId.ToString());
        var ownerInbox = new HubEventInbox<MessageDto>(owner, "MessageReceived");

        var join = await Assert.ThrowsAsync<HubException>(() => outsider.InvokeAsync("JoinRoom", roomId.ToString()));
        var send = await Assert.ThrowsAsync<HubException>(() =>
            outsider.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "let me in"));

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(join));
        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(send));
        Assert.Equal(0, await HubFixtures.StoredCountAsync(factory, "let me in"));
        await ownerInbox.AssertSilenceAsync();
    }

    [Fact]
    public async Task MalformedOrMissingConversation_IsNotFound()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("bad-id"));
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);

        var malformed = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<MessageDto>("SendMessage", "not-a-guid", "x"));
        var missing = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<MessageDto>("SendMessage", Guid.NewGuid().ToString(), "x"));

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(malformed));
        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(missing));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmptyText_IsInvalidMessage(string? text)
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("empty-text"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);

        var error = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), text!));

        Assert.Equal(HubErrorCodes.InvalidMessage, HubTestErrors.CodeOf(error));
    }

    [Fact]
    public async Task TooLongText_IsInvalidMessage()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("long-text"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);
        var tooLong = new string('x', SignalChat.Domain.Chat.Message.MaxTextLength + 1);

        var error = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), tooLong));

        Assert.Equal(HubErrorCodes.InvalidMessage, HubTestErrors.CodeOf(error));
    }

    [Fact]
    public async Task TwentyFirstMessageInTheWindow_IsRateLimited()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rate"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);

        for (var sent = 0; sent < ChatHubLimits.SendRateLimitPermits; sent++)
        {
            await connection.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), $"m{sent}");
        }
        var error = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "one too many"));

        Assert.Equal(HubErrorCodes.RateLimited, HubTestErrors.CodeOf(error));
        Assert.Equal(0, await HubFixtures.StoredCountAsync(factory, "one too many"));
    }

    [Fact]
    public async Task AfterLeavingOverRest_TheFormerMemberStopsReceivingMessages()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("left-owner"));
        var (_, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("left-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var owner = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var peer = await HubTestClient.ConnectAsync(factory, peerCookie);
        await owner.InvokeAsync("JoinRoom", roomId.ToString());
        await peer.InvokeAsync("JoinRoom", roomId.ToString());
        var peerInbox = new HubEventInbox<MessageDto>(peer, "MessageReceived");

        await HubFixtures.LeaveRoomOverRestAsync(client, peerCookie, roomId);
        await owner.InvokeAsync("SendMessage", roomId.ToString(), "after you left");

        await peerInbox.AssertSilenceAsync();
    }

    [Fact]
    public async Task JoinAndLeave_NotifyTheOtherMembers()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ev-owner"));
        var (peerUser, peerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ev-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, peerCookie, roomId);
        await using var owner = await HubTestClient.ConnectAsync(factory, ownerCookie);
        await using var peer = await HubTestClient.ConnectAsync(factory, peerCookie);
        await owner.InvokeAsync("JoinRoom", roomId.ToString());
        var joinedInbox = new HubEventInbox<ConversationMemberEventDto>(owner, "UserJoined");
        var leftInbox = new HubEventInbox<ConversationMemberEventDto>(owner, "UserLeft");

        await peer.InvokeAsync("JoinRoom", roomId.ToString());
        var joined = await joinedInbox.NextAsync();
        await peer.InvokeAsync("LeaveRoom", roomId.ToString());
        var left = await leftInbox.NextAsync();

        Assert.Equal(roomId, joined.ConversationId);
        Assert.Equal(peerUser.Id, joined.User.Id);
        Assert.Equal(peerUser.Id, left.User.Id);
    }
}

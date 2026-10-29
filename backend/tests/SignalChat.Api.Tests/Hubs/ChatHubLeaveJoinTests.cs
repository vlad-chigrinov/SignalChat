using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>REST leave and rejoin while the hub connection stays open: the state of the connection follows membership.</summary>
public sealed class ChatHubLeaveJoinTests
{
    [Fact]
    public async Task AfterRestLeaveAndJoin_TheSameConnectionReceivesLiveMessagesAgain()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rejoin-alice"));
        var (_, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rejoin-bob"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, aliceCookie);
        await HubFixtures.JoinRoomAsync(client, bobCookie, roomId);
        await using var alice = await HubTestClient.ConnectAsync(factory, aliceCookie);
        await using var bob = await HubTestClient.ConnectAsync(factory, bobCookie);
        await alice.InvokeAsync("JoinRoom", roomId.ToString());
        await bob.InvokeAsync("JoinRoom", roomId.ToString());

        await HubFixtures.LeaveRoomOverRestAsync(client, bobCookie, roomId);
        await HubFixtures.JoinRoomAsync(client, bobCookie, roomId);
        await bob.InvokeAsync("JoinRoom", roomId.ToString());
        var bobInbox = new HubEventInbox<MessageDto>(bob, "MessageReceived");

        await alice.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "after rejoin");

        Assert.Equal("after rejoin", (await bobInbox.NextAsync()).Text);
    }

    [Fact]
    public async Task RestLeave_TellsTheRemainingMembers_NotTheLeaver()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("left-event-alice"));
        var (bobUser, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("left-event-bob"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, aliceCookie);
        await HubFixtures.JoinRoomAsync(client, bobCookie, roomId);
        await using var alice = await HubTestClient.ConnectAsync(factory, aliceCookie);
        await using var bob = await HubTestClient.ConnectAsync(factory, bobCookie);
        await alice.InvokeAsync("JoinRoom", roomId.ToString());
        await bob.InvokeAsync("JoinRoom", roomId.ToString());
        var aliceLeft = new HubEventInbox<ConversationMemberEventDto>(alice, "UserLeft");
        var bobLeft = new HubEventInbox<ConversationMemberEventDto>(bob, "UserLeft");

        await HubFixtures.LeaveRoomOverRestAsync(client, bobCookie, roomId);

        var left = await aliceLeft.NextAsync();
        Assert.Equal(roomId, left.ConversationId);
        Assert.Equal(bobUser.Id, left.User.Id);
        await bobLeft.AssertSilenceAsync();
    }
}

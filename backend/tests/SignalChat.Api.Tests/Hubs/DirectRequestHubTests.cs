using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using HubException = Microsoft.AspNetCore.SignalR.HubException;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>The consent flow over live connections: the events, and what a request lets each side do before the answer.</summary>
public sealed class DirectRequestHubTests
{
    [Fact]
    public async Task NewRequest_IsPushedLiveToTheRecipient()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-req-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-req-bob"));
        using var client = factory.CreateApiClient();
        await using var bobHub = await HubTestClient.ConnectAsync(factory, bobCookie);
        var received = new HubEventInbox<DirectRequestReceivedDto>(bobHub, "DirectRequestReceived");

        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hi there");

        var request = await received.NextAsync();
        Assert.Equal(conversationId, request.ConversationId);
        Assert.Equal(alice.Id, request.From.Id);
        Assert.Equal("Hi there", request.Text);
    }

    [Fact]
    public async Task Accept_ReachesTheInitiator_AndOnlyThenTheyCanChat()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-acc-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-acc-bob"));
        using var client = factory.CreateApiClient();
        await using var aliceHub = await HubTestClient.ConnectAsync(factory, aliceCookie);
        await using var bobHub = await HubTestClient.ConnectAsync(factory, bobCookie);
        var accepted = new HubEventInbox<DirectRequestAcceptedDto>(aliceHub, "DirectRequestAccepted");
        var bobMessages = new HubEventInbox<MessageDto>(bobHub, "MessageReceived");
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hi there");
        var conversation = conversationId.ToString();

        var early = await Assert.ThrowsAsync<HubException>(
            () => aliceHub.InvokeAsync<MessageDto>("SendMessage", conversation, "too early"));
        var notJoined = await Assert.ThrowsAsync<HubException>(() => bobHub.InvokeAsync("JoinRoom", conversation));
        await bobMessages.AssertSilenceAsync();
        await DirectRequestFixtures.AcceptAsync(client, bobCookie, conversationId);

        var acceptance = await accepted.NextAsync();
        await aliceHub.InvokeAsync("JoinRoom", conversation);
        await bobHub.InvokeAsync("JoinRoom", conversation);
        await aliceHub.InvokeAsync<MessageDto>("SendMessage", conversation, "hello");
        var delivered = await bobMessages.NextAsync();

        Assert.Equal(DirectRequestErrorCodes.NotAccepted, HubTestErrors.CodeOf(early));
        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(notJoined));
        Assert.Equal(conversationId, acceptance.ConversationId);
        Assert.Equal(bob.Id, acceptance.Peer.Id);
        Assert.Equal("hello", delivered.Text);
    }

    [Fact]
    public async Task Decline_IsSilent_AndTheDeclinerMayWriteFirstLater()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-dec-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("live-dec-bob"));
        using var client = factory.CreateApiClient();
        await using var aliceHub = await HubTestClient.ConnectAsync(factory, aliceCookie);
        await using var bobHub = await HubTestClient.ConnectAsync(factory, bobCookie);
        var aliceAccepted = new HubEventInbox<DirectRequestAcceptedDto>(aliceHub, "DirectRequestAccepted");
        var aliceReceived = new HubEventInbox<DirectRequestReceivedDto>(aliceHub, "DirectRequestReceived");
        var bobReceived = new HubEventInbox<DirectRequestReceivedDto>(bobHub, "DirectRequestReceived");
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hi there");
        await bobReceived.NextAsync();

        await DirectRequestFixtures.DeclineAsync(client, bobCookie, conversationId);
        await aliceAccepted.AssertSilenceAsync();
        await aliceReceived.AssertSilenceAsync();
        await DirectRequestFixtures.RequestAsync(client, bobCookie, alice.Id, "Sorry, hi");

        var reopened = await aliceReceived.NextAsync();
        Assert.Equal(conversationId, reopened.ConversationId);
        Assert.Equal(bob.Id, reopened.From.Id);
        Assert.Equal("Sorry, hi", reopened.Text);
    }
}

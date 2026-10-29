using System.Net;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Tests.Chat;

/// <summary>A direct message is a request until the peer accepts: who sees what, and what answers look like.</summary>
public sealed class DirectRequestRestTests
{
    [Fact]
    public async Task NewRequest_IsPending_VisibleToTheInitiator_AndWaitingForThePeer()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("req-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("req-bob"));
        using var client = factory.CreateApiClient();

        using var created = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id, text = "Hello Bob" }));
        var conversation = await ChatHttp.ReadJsonAsync(created);
        var conversationId = conversation.GetProperty("id").GetGuid();
        var aliceList = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-conversations", aliceCookie)));
        var bobList = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-conversations", bobCookie)));
        var bobIncoming = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-requests/incoming", bobCookie)));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("pending", conversation.GetProperty("status").GetString());
        Assert.Contains(aliceList.EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == conversationId && item.GetProperty("status").GetString() == "pending");
        Assert.DoesNotContain(bobList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == conversationId);
        var incoming = Assert.Single(bobIncoming.EnumerateArray());
        Assert.Equal(conversationId, incoming.GetProperty("id").GetGuid());
        Assert.Equal("Hello Bob", incoming.GetProperty("text").GetString());
        Assert.Equal(alice.Id, incoming.GetProperty("from").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task History_IsReadableByTheInitiator_AndHiddenFromThePeerUntilAccepted()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("hist-req-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("hist-req-bob"));
        using var client = factory.CreateApiClient();
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hello Bob");

        var fromAlice = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages", aliceCookie));
        var fromBob = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages", bobCookie));
        Assert.Equal(HttpStatusCode.OK, fromAlice.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fromBob.StatusCode);

        await DirectRequestFixtures.AcceptAsync(client, bobCookie, conversationId);
        var afterAccept = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages", bobCookie));
        Assert.Equal(HttpStatusCode.OK, afterAccept.StatusCode);
    }

    [Fact]
    public async Task OnlyTheRecipient_CanAccept_AndAnswersToAnotherStateAreNotFound()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ans-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ans-bob"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("ans-stranger"));
        using var client = factory.CreateApiClient();
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hello Bob");

        var byInitiator = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{conversationId}/accept", aliceCookie));
        var byStranger = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{conversationId}/decline", strangerCookie));
        await DirectRequestFixtures.AcceptAsync(client, bobCookie, conversationId);
        var acceptedAgain = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{conversationId}/accept", bobCookie));

        Assert.Equal(HttpStatusCode.NotFound, byInitiator.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, acceptedAgain.StatusCode);
    }

    [Fact]
    public async Task Decline_IsSilentToTheInitiator_RepeatIsPending_AndTheDeclinerMayWriteFirst()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("dec-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("dec-bob"));
        using var client = factory.CreateApiClient();
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "Hello Bob");

        await DirectRequestFixtures.DeclineAsync(client, bobCookie, conversationId);
        var aliceList = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-conversations", aliceCookie)));
        var repeat = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id, text = "Again" }));
        var bobHistory = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages", bobCookie));
        var reopened = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", bobCookie, new { userId = alice.Id, text = "Sorry, hi" }));

        // The initiator still sees its request as pending, and the repeat gets the same answer as a pending request.
        Assert.Contains(aliceList.EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == conversationId && item.GetProperty("status").GetString() == "pending");
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(DirectRequestErrorCodes.RequestPending, (await ChatHttp.ReadJsonAsync(repeat)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, bobHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reopened.StatusCode);
        Assert.Equal("pending", (await ChatHttp.ReadJsonAsync(reopened)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ARequestWithoutText_IsRefused_WithInvalidMessage()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("notext-alice"));
        var (bob, _) = await factory.CreateSessionAsync(HubFixtures.Profile("notext-bob"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_message", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task OutgoingRequests_AreLimitedToTwentyWaitingAtATime()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("limit-alice"));
        using var client = factory.CreateApiClient();
        for (var index = 0; index < DirectConversation.MaxOutgoingPendingPerUser; index++)
        {
            var (peer, _) = await factory.CreateSessionAsync(HubFixtures.Profile($"limit-peer-{index}"));
            await DirectRequestFixtures.RequestAsync(client, aliceCookie, peer.Id, $"Request {index}");
        }
        var (overflow, _) = await factory.CreateSessionAsync(HubFixtures.Profile("limit-overflow"));

        var response = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = overflow.Id, text = "One too many" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(DirectRequestErrorCodes.RequestLimitReached, (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }
}

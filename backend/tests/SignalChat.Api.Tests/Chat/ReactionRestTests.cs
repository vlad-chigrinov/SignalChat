using System.Net;
using System.Net.Http.Json;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using static SignalChat.Api.Tests.Chat.ReactionFixtures;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Choosing, replacing and removing a reaction: one reaction per user and message, held by the server.</summary>
public sealed class ReactionRestTests
{
    [Fact]
    public async Task AMember_CanReact_AndSeesTheirOwnReactionMarked()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-set"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        var reactions = await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);

        Assert.Equal([new ShownReaction(ThumbsUp, 1, true)], reactions);
    }

    [Fact]
    public async Task ChoosingAnotherReaction_ReplacesTheFirst_AndKeepsOneRow()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-replace"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);

        var reactions = await ReactOkAsync(client, cookie, roomId, messageId, Heart);

        Assert.Equal([new ShownReaction(Heart, 1, true)], reactions);
        Assert.Equal(1, await StoredReactionCountAsync(factory, messageId));
    }

    [Fact]
    public async Task ChoosingTheSameReactionAgain_RemovesIt()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-remove"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);

        var reactions = await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);

        Assert.Empty(reactions);
        Assert.Equal(0, await StoredReactionCountAsync(factory, messageId));
    }

    [Fact]
    public async Task TwoUsersWithTheSameReaction_AreCounted_AndOnlyOwnIsMarkedForEach()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-two-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-two-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        var seenByMate = await ReactOkAsync(client, mateCookie, roomId, messageId, ThumbsUp);
        var seenByOwner = await ReactOkAsync(client, ownerCookie, roomId, messageId, Heart);

        Assert.Equal([new ShownReaction(ThumbsUp, 2, true)], seenByMate);
        Assert.Equal(
            [new ShownReaction(ThumbsUp, 1, false), new ShownReaction(Heart, 1, true)],
            seenByOwner);
        Assert.Equal(2, await StoredReactionCountAsync(factory, messageId));
    }

    [Theory]
    [InlineData("💩")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("👍👍")]
    [InlineData("👍 ")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AReactionOutsideTheCatalog_IsRefused_AndNothingIsStored(string? emoji)
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-bad"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        using var response = await ReactAsync(client, cookie, roomId, messageId, emoji);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_reaction", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(0, await StoredReactionCountAsync(factory, messageId));
    }

    [Fact]
    public async Task ARequestWithoutBody_IsRefused()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-nobody"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        using var response = await client.SendAsync(ChatHttp.Post(Path(roomId, messageId), cookie));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ANonMember_GetsNotFound_AndNothingIsStored()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-out-a"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-out-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        using var response = await ReactAsync(client, strangerCookie, roomId, messageId, ThumbsUp);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await StoredReactionCountAsync(factory, messageId));
    }

    [Fact]
    public async Task AMemberWhoLeft_CanNoLongerReact()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-left-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-left-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await HubFixtures.LeaveRoomOverRestAsync(client, mateCookie, roomId);

        using var response = await ReactAsync(client, mateCookie, roomId, messageId, ThumbsUp);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMessageOfAnotherConversation_IsNotFound_EvenForAMemberOfBoth()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-cross"));
        using var client = factory.CreateApiClient();
        var firstRoom = await HubFixtures.CreateRoomAsync(client, cookie);
        var secondRoom = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageInSecond = await AppendMessageAsync(factory, owner.Id, secondRoom);

        using var response = await ReactAsync(client, cookie, firstRoom, messageInSecond, ThumbsUp);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await StoredReactionCountAsync(factory, messageInSecond));
    }

    [Fact]
    public async Task AMissingOrMalformedMessage_IsNotFound()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-missing"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);

        using var missing = await ReactAsync(client, cookie, roomId, Guid.NewGuid(), ThumbsUp);
        using var malformed = await client.SendAsync(
            ChatHttp.Post($"/api/conversations/{roomId}/messages/not-a-guid/reaction", cookie, new { emoji = ThumbsUp }));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task ADeletedMessage_CannotBeReactedTo()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-deleted"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactionDatabase.SoftDeleteAsync(factory, messageId);

        using var response = await ReactAsync(client, cookie, roomId, messageId, ThumbsUp);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APendingRequest_AllowsNoReactions_ToItsInitiatorOrItsRecipient()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-pend-a"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-pend-b"));
        using var client = factory.CreateApiClient();
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "may I?");
        var requestMessageId = await ReactionDatabase.FirstMessageIdAsync(factory, conversationId);

        using var fromInitiator = await ReactAsync(client, aliceCookie, conversationId, requestMessageId, ThumbsUp);
        using var fromRecipient = await ReactAsync(client, bobCookie, conversationId, requestMessageId, ThumbsUp);

        Assert.Equal(HttpStatusCode.Conflict, fromInitiator.StatusCode);
        Assert.Equal("dm_not_accepted", (await ChatHttp.ReadJsonAsync(fromInitiator)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, fromRecipient.StatusCode);
        Assert.Equal(0, await StoredReactionCountAsync(factory, requestMessageId));
    }

    [Fact]
    public async Task ADeclinedRequest_AllowsNoReactions()
    {
        using var factory = new SignalChatApiFactory();
        var (_, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-decl-a"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-decl-b"));
        using var client = factory.CreateApiClient();
        var conversationId = await DirectRequestFixtures.RequestAsync(client, aliceCookie, bob.Id, "may I?");
        var requestMessageId = await ReactionDatabase.FirstMessageIdAsync(factory, conversationId);
        await DirectRequestFixtures.DeclineAsync(client, bobCookie, conversationId);

        using var fromRecipient = await ReactAsync(client, bobCookie, conversationId, requestMessageId, ThumbsUp);
        using var fromInitiator = await ReactAsync(client, aliceCookie, conversationId, requestMessageId, ThumbsUp);

        Assert.False(fromRecipient.IsSuccessStatusCode);
        Assert.False(fromInitiator.IsSuccessStatusCode);
        Assert.Equal(0, await StoredReactionCountAsync(factory, requestMessageId));
    }

    [Fact]
    public async Task InAnAcceptedDirectChat_BothSidesCanReact()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-dm-a"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-dm-b"));
        using var client = factory.CreateApiClient();
        var conversationId = await CreateDirectAcceptedAsync(client, aliceCookie, bobCookie, bob.Id);
        var messageId = await AppendMessageAsync(factory, alice.Id, conversationId);

        await ReactOkAsync(client, aliceCookie, conversationId, messageId, ThumbsUp);
        var seenByBob = await ReactOkAsync(client, bobCookie, conversationId, messageId, ThumbsUp);

        Assert.Equal([new ShownReaction(ThumbsUp, 2, true)], seenByBob);
    }

    [Fact]
    public async Task AVisitorWithoutASession_GetsUnauthorized()
    {
        using var factory = new SignalChatApiFactory();
        using var client = factory.CreateApiClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Path(Guid.NewGuid(), Guid.NewGuid()))
        {
            Content = JsonContent.Create(new { emoji = ThumbsUp }),
        };
        request.Headers.Add("Origin", SignalChatApiFactory.AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

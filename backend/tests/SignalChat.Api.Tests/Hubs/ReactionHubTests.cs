using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;
using static SignalChat.Api.Tests.Chat.ReactionFixtures;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>
/// A reaction change reaches the members of the conversation as counts only. The "which one is mine" part goes to
/// the actor's own tabs alone, so no event names who reacted.
/// </summary>
public sealed class ReactionHubTests
{
    private const string CountsEvent = "ReactionsChanged";
    private const string MineEvent = "MyReactionChanged";

    [Fact]
    public async Task RoomMembers_AreToldTheNewCounts()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await using var mateHub = await ConnectAndJoinAsync(factory, mateCookie, roomId);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(mateHub, CountsEvent);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        var change = await counts.NextAsync();

        Assert.Equal(roomId, change.ConversationId);
        Assert.Equal(messageId, change.MessageId);
        Assert.Equal([new ReactionCountDto(ThumbsUp, 1)], change.Reactions);
    }

    [Fact]
    public async Task ReplacingAReaction_SendsTheWholeNewCounts()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-rep-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-rep-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        await ReactOkAsync(client, mateCookie, roomId, messageId, ThumbsUp);
        await using var mateHub = await ConnectAndJoinAsync(factory, mateCookie, roomId);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(mateHub, CountsEvent);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, Heart);
        var change = await counts.NextAsync();

        Assert.Equal([new ReactionCountDto(ThumbsUp, 1), new ReactionCountDto(Heart, 1)], change.Reactions);
    }

    [Fact]
    public async Task RemovingTheLastReaction_SendsAnEmptyList()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-clr-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-clr-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        await using var mateHub = await ConnectAndJoinAsync(factory, mateCookie, roomId);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(mateHub, CountsEvent);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        var change = await counts.NextAsync();

        Assert.Empty(change.Reactions);
    }

    [Fact]
    public void TheCountsEvent_NamesNoUser()
    {
        var fields = typeof(ReactionsChangedEventDto).GetProperties().Select(property => property.Name).Order();

        Assert.Equal(["ConversationId", "MessageId", "Reactions"], fields);
    }

    [Fact]
    public async Task DirectChatPeer_IsToldTheNewCounts()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-dm-a"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-dm-b"));
        using var client = factory.CreateApiClient();
        var conversationId = await CreateDirectAcceptedAsync(client, aliceCookie, bobCookie, bob.Id);
        var messageId = await AppendMessageAsync(factory, alice.Id, conversationId);
        await using var bobHub = await ConnectAndJoinAsync(factory, bobCookie, conversationId);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(bobHub, CountsEvent);

        await ReactOkAsync(client, aliceCookie, conversationId, messageId, Laughing);
        var change = await counts.NextAsync();

        Assert.Equal([new ReactionCountDto(Laughing, 1)], change.Reactions);
    }

    [Fact]
    public async Task TheActorsOtherTab_LearnsWhichReactionIsMine()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-mine"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await using var otherTab = await ConnectAndJoinAsync(factory, cookie, roomId);
        var mine = new HubEventInbox<MyReactionChangedEventDto>(otherTab, MineEvent);

        await ReactOkAsync(client, cookie, roomId, messageId, Heart);
        var chosen = await mine.NextAsync();
        await ReactOkAsync(client, cookie, roomId, messageId, Heart);
        var removed = await mine.NextAsync();

        Assert.Equal(new MyReactionChangedEventDto(roomId, messageId, Heart), chosen);
        Assert.Equal(new MyReactionChangedEventDto(roomId, messageId, null), removed);
    }

    [Fact]
    public async Task AnotherMember_NeverLearnsWhichReactionIsSomeoneElses()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-priv-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-priv-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await using var mateHub = await ConnectAndJoinAsync(factory, mateCookie, roomId);
        var mine = new HubEventInbox<MyReactionChangedEventDto>(mateHub, MineEvent);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(mateHub, CountsEvent);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        await counts.NextAsync();

        await mine.AssertSilenceAsync();
    }

    [Fact]
    public async Task ANonMemberConnection_IsToldNothing()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-out-a"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-out-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await using var strangerHub = await HubTestClient.ConnectAsync(factory, strangerCookie);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(strangerHub, CountsEvent);

        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);

        await counts.AssertSilenceAsync();
    }

    [Fact]
    public async Task ARefusedReaction_SendsNothing()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rhub-refused"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await using var hub = await ConnectAndJoinAsync(factory, cookie, roomId);
        var counts = new HubEventInbox<ReactionsChangedEventDto>(hub, CountsEvent);
        var mine = new HubEventInbox<MyReactionChangedEventDto>(hub, MineEvent);

        using var response = await ReactAsync(client, cookie, roomId, messageId, "💩");

        Assert.False(response.IsSuccessStatusCode);
        await counts.AssertSilenceAsync();
        await mine.AssertSilenceAsync();
    }

    private static async Task<HubConnection> ConnectAndJoinAsync(SignalChatApiFactory factory, string cookie, Guid conversationId)
    {
        var connection = await HubTestClient.ConnectAsync(factory, cookie);
        await connection.InvokeAsync("JoinRoom", conversationId.ToString());
        return connection;
    }
}

using System.Text.Json;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using static SignalChat.Api.Tests.Chat.ReactionFixtures;

namespace SignalChat.Api.Tests.Chat;

/// <summary>History carries the reactions of each message as counts plus a "reacted by me" flag, and nothing about who else reacted.</summary>
public sealed class ReactionHistoryTests
{
    private static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task History_ShowsCountsAndTheViewersOwnReaction()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, ownerCookie, roomId, messageId, ThumbsUp);
        await ReactOkAsync(client, mateCookie, roomId, messageId, ThumbsUp);
        await ReactOkAsync(client, mateCookie, roomId, messageId, Laughing);
        await ReactOkAsync(client, ownerCookie, roomId, messageId, Heart);

        var forOwner = await ReadReactionsAsync(client, ownerCookie, roomId);
        var forMate = await ReadReactionsAsync(client, mateCookie, roomId);

        Assert.Equal([new ShownReaction(Heart, 1, true), new ShownReaction(Laughing, 1, false)], forOwner[messageId]);
        Assert.Equal([new ShownReaction(Heart, 1, false), new ShownReaction(Laughing, 1, true)], forMate[messageId]);
    }

    [Fact]
    public async Task AMessageWithoutReactions_HasAnEmptyList()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-empty"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);

        var reactions = await ReadReactionsAsync(client, cookie, roomId);

        Assert.Empty(reactions[messageId]);
    }

    [Fact]
    public async Task ReactionsAreListedInCatalogOrder_NotInTheOrderTheyWereChosen()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-order-a"));
        var (_, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-order-b"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, roomId);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, ownerCookie, roomId, messageId, Laughing);
        await ReactOkAsync(client, mateCookie, roomId, messageId, ThumbsUp);

        var reactions = await ReadReactionsAsync(client, ownerCookie, roomId);

        Assert.Equal([ThumbsUp, Laughing], reactions[messageId].Select(reaction => reaction.Emoji));
    }

    [Fact]
    public async Task EveryPageOfHistory_CarriesTheReactionsOfItsOwnMessages()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-pages"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var ids = new List<Guid>();
        for (var number = 0; number < 4; number++)
        {
            factory.Clock.Advance(OneMillisecond);
            ids.Add(await AppendMessageAsync(factory, owner.Id, roomId, $"m{number}"));
        }
        await ReactOkAsync(client, cookie, roomId, ids[0], ThumbsUp);
        await ReactOkAsync(client, cookie, roomId, ids[3], Heart);

        var newest = await ReadPageAsync(client, cookie, roomId, "?limit=2");
        var older = await ReadPageAsync(client, cookie, roomId, $"?limit=2&before={newest.GetProperty("nextCursor").GetString()}");

        Assert.Equal([ids[2], ids[3]], MessageIds(newest));
        Assert.Equal([new ShownReaction(Heart, 1, true)], ReactionsOf(newest, ids[3]));
        Assert.Empty(ReactionsOf(newest, ids[2]));
        Assert.Equal([ids[0], ids[1]], MessageIds(older));
        Assert.Equal([new ShownReaction(ThumbsUp, 1, true)], ReactionsOf(older, ids[0]));
    }

    [Fact]
    public async Task AReaction_ExposesOnlyEmojiCountAndTheOwnFlag_NeverWhoReacted()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-privacy"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);

        var page = await ReadPageAsync(client, cookie, roomId, "?limit=10");
        var reaction = page.GetProperty("items")[0].GetProperty("reactions")[0];

        Assert.Equal(["count", "emoji", "reactedByMe"], reaction.EnumerateObject().Select(field => field.Name).Order());
    }

    [Fact]
    public async Task ReactionsOfASoftDeletedMessage_AreNotShown()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rh-deleted"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await AppendMessageAsync(factory, owner.Id, roomId);
        await ReactOkAsync(client, cookie, roomId, messageId, ThumbsUp);
        await ReactionDatabase.SoftDeleteAsync(factory, messageId);

        var reactions = await ReadReactionsAsync(client, cookie, roomId);

        Assert.DoesNotContain(messageId, reactions.Keys);
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<ShownReaction>>> ReadReactionsAsync(
        HttpClient client, string cookie, Guid conversationId)
    {
        var page = await ReadPageAsync(client, cookie, conversationId, "?limit=50");
        return page.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => ReadReactions(item.GetProperty("reactions")));
    }

    private static async Task<JsonElement> ReadPageAsync(HttpClient client, string cookie, Guid conversationId, string query)
    {
        using var response = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages{query}", cookie));
        response.EnsureSuccessStatusCode();
        return await ChatHttp.ReadJsonAsync(response);
    }

    private static IReadOnlyList<Guid> MessageIds(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

    private static IReadOnlyList<ShownReaction> ReactionsOf(JsonElement page, Guid messageId) =>
        ReadReactions(page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == messageId)
            .GetProperty("reactions"));
}

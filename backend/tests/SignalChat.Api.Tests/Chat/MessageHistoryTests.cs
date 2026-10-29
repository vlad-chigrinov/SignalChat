using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Chat;

/// <summary>History paging needs a controllable clock, so each test gets its own host.</summary>
public sealed class MessageHistoryTests
{
    private const int MessageCount = 75;
    private const int PageSize = 30;
    private static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task History_ThreePages_AreOrderedAndHaveNoGapsOrDuplicates()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(Profile("history-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        await AppendSequenceAsync(factory, owner.Id, roomId, MessageCount);

        var first = await ReadPageAsync(client, ownerCookie, roomId, $"?limit={PageSize}");
        var second = await ReadPageAsync(client, ownerCookie, roomId, $"?limit={PageSize}&before={first.NextCursor}");
        var third = await ReadPageAsync(client, ownerCookie, roomId, $"?limit={PageSize}&before={second.NextCursor}");

        Assert.Null(third.NextCursor);
        var allTexts = third.Texts.Concat(second.Texts).Concat(first.Texts).ToList();
        Assert.Equal(Enumerable.Range(1, MessageCount).Select(number => $"m{number}"), allTexts);
        Assert.Equal(PageSize, first.Texts.Count);
    }

    [Fact]
    public async Task History_FirstPageIsTheNewestMessages_InChronologicalOrder()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(Profile("newest-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        await AppendSequenceAsync(factory, owner.Id, roomId, MessageCount);

        var page = await ReadPageAsync(client, ownerCookie, roomId, $"?limit={PageSize}");

        Assert.Equal($"m{MessageCount - PageSize + 1}", page.Texts.First());
        Assert.Equal($"m{MessageCount}", page.Texts.Last());
    }

    [Fact]
    public async Task History_DirectConversation_IsReadableByBothParticipants()
    {
        using var factory = new SignalChatApiFactory();
        var (alice, aliceCookie) = await factory.CreateSessionAsync(Profile("dm-hist-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(Profile("dm-hist-bob"));
        using var client = factory.CreateApiClient();
        var created = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id })));
        var conversationId = created.GetProperty("id").GetGuid();
        await AppendSequenceAsync(factory, alice.Id, conversationId, 3);

        var fromBob = await ReadPageAsync(client, bobCookie, conversationId, "?limit=10");

        Assert.Equal(new[] { "m1", "m2", "m3" }, fromBob.Texts);
    }

    [Fact]
    public async Task History_ForNonMemberMissingOrMalformedConversation_Is404()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(Profile("probe-owner"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(Profile("probe-stranger"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        await AppendSequenceAsync(factory, owner.Id, roomId, 1);

        var foreignRoom = await client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages", strangerCookie));
        var missing = await client.SendAsync(ChatHttp.Get($"/api/conversations/{Guid.NewGuid()}/messages", ownerCookie));
        var malformed = await client.SendAsync(ChatHttp.Get("/api/conversations/not-a-guid/messages", ownerCookie));

        Assert.Equal(HttpStatusCode.NotFound, foreignRoom.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task History_WithBrokenCursorOrBadLimit_Returns400WithCode()
    {
        using var factory = new SignalChatApiFactory();
        var (_, ownerCookie) = await factory.CreateSessionAsync(Profile("bad-input-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);

        var brokenCursor = await client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages?before=%%%", ownerCookie));
        var zeroLimit = await client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages?limit=0", ownerCookie));

        Assert.Equal("invalid_cursor", (await ChatHttp.ReadJsonAsync(brokenCursor)).GetProperty("code").GetString());
        Assert.Equal("invalid_limit", (await ChatHttp.ReadJsonAsync(zeroLimit)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, zeroLimit.StatusCode);
    }

    [Fact]
    public async Task History_HidesSoftDeletedMessages()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, ownerCookie) = await factory.CreateSessionAsync(Profile("delete-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        var messageIds = await AppendSequenceAsync(factory, owner.Id, roomId, 3);
        await SoftDeleteAsync(factory, messageIds[1]);

        var page = await ReadPageAsync(client, ownerCookie, roomId, "?limit=10");

        Assert.Equal(new[] { "m1", "m3" }, page.Texts);
    }

    private static async Task<IReadOnlyList<Guid>> AppendSequenceAsync(
        SignalChatApiFactory factory, Guid authorId, Guid conversationId, int count)
    {
        var ids = new List<Guid>();
        for (var number = 1; number <= count; number++)
        {
            factory.Clock.Advance(OneMillisecond);
            using var scope = factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();
            var view = await store.AppendAsync(authorId, conversationId, $"m{number}", CancellationToken.None);
            ids.Add(view.Id);
        }
        return ids;
    }

    private static async Task SoftDeleteAsync(SignalChatApiFactory factory, Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        var message = await db.Messages.SingleAsync(item => item.Id == messageId);
        message.Delete(factory.Clock.GetUtcNow());
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateRoomAsync(HttpClient client, string cookie)
    {
        var response = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = ChatHttp.UniqueName("History"), description = "" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ChatHttp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<(IReadOnlyList<string> Texts, string? NextCursor)> ReadPageAsync(
        HttpClient client, string cookie, Guid conversationId, string query)
    {
        var response = await client.SendAsync(ChatHttp.Get($"/api/conversations/{conversationId}/messages{query}", cookie));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ChatHttp.ReadJsonAsync(response);
        var texts = body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("text").GetString()!).ToList();
        var next = body.GetProperty("nextCursor");
        return (texts, next.ValueKind == System.Text.Json.JsonValueKind.Null ? null : next.GetString());
    }

    private static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", null);
}

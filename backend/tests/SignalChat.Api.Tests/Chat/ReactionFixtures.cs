using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Steps shared by the reaction tests: a stored message, the reaction call and what ended up in the database.</summary>
internal static class ReactionFixtures
{
    public const string ThumbsUp = "👍";
    public const string Heart = "❤️";
    public const string Laughing = "😂";

    public static async Task<Guid> AppendMessageAsync(SignalChatApiFactory factory, Guid authorId, Guid conversationId, string text = "hello")
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();
        var view = await store.AppendAsync(authorId, conversationId, text, CancellationToken.None);
        return view.Id;
    }

    public static string Path(Guid conversationId, Guid messageId) =>
        $"/api/conversations/{conversationId}/messages/{messageId}/reaction";

    public static Task<HttpResponseMessage> ReactAsync(
        HttpClient client, string cookie, Guid conversationId, Guid messageId, string? emoji) =>
        client.SendAsync(ChatHttp.Post(Path(conversationId, messageId), cookie, new { emoji }));

    /// <summary>Reacts and expects success; returns the reactions of the message as the caller sees them.</summary>
    public static async Task<IReadOnlyList<ShownReaction>> ReactOkAsync(
        HttpClient client, string cookie, Guid conversationId, Guid messageId, string emoji)
    {
        using var response = await ReactAsync(client, cookie, conversationId, messageId, emoji);
        response.EnsureSuccessStatusCode();
        return ReadReactions((await ChatHttp.ReadJsonAsync(response)).GetProperty("reactions"));
    }

    public static IReadOnlyList<ShownReaction> ReadReactions(JsonElement reactions) =>
        reactions.EnumerateArray()
            .Select(item => new ShownReaction(
                item.GetProperty("emoji").GetString()!,
                item.GetProperty("count").GetInt32(),
                item.GetProperty("reactedByMe").GetBoolean()))
            .ToList();

    public static async Task<int> StoredReactionCountAsync(SignalChatApiFactory factory, Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        return await db.MessageReactions.CountAsync(reaction => reaction.MessageId == messageId);
    }

    public static async Task<Guid> CreateDirectAcceptedAsync(
        HttpClient client, string initiatorCookie, string peerCookie, Guid peerId)
    {
        var conversationId = await DirectRequestFixtures.RequestAsync(client, initiatorCookie, peerId, "hi");
        await DirectRequestFixtures.AcceptAsync(client, peerCookie, conversationId);
        return conversationId;
    }
}

/// <summary>One reaction under a message, as the REST responses show it.</summary>
internal sealed record ShownReaction(string Emoji, int Count, bool ReactedByMe);

using System.Net;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Chat;

public sealed class DirectAndSearchRestTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task DirectConversation_IsARequestUntilAccepted_ThenSymmetricAndIdempotent()
    {
        var (alice, aliceCookie) = await factory.CreateSessionAsync(Profile("dm-alice"));
        var (bob, bobCookie) = await factory.CreateSessionAsync(Profile("dm-bob"));
        using var client = factory.CreateApiClient();

        using var requested = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id, text = "hi" }));
        var waiting = await ChatHttp.ReadJsonAsync(requested);
        using var incomingForBob = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", bobCookie, new { userId = alice.Id, text = "hi" }));
        using var accepted = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{waiting.GetProperty("id").GetGuid()}/accept", bobCookie));
        var fromAlice = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id, text = "again" })));
        var fromBob = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Post("/api/direct-conversations", bobCookie, new { userId = alice.Id, text = "again" })));

        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        Assert.Equal("pending", waiting.GetProperty("status").GetString());
        Assert.Equal("dm_incoming_request_exists", (await ChatHttp.ReadJsonAsync(incomingForBob)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal("direct", fromAlice.GetProperty("kind").GetString());
        Assert.Equal("accepted", fromAlice.GetProperty("status").GetString());
        Assert.Equal(fromAlice.GetProperty("id").GetGuid(), fromBob.GetProperty("id").GetGuid());
        Assert.Equal(bob.Id, fromAlice.GetProperty("peer").GetProperty("id").GetGuid());
        Assert.Equal(alice.Id, fromBob.GetProperty("peer").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task DirectConversation_WithSelf_Returns400WithCode()
    {
        var (alice, aliceCookie) = await factory.CreateSessionAsync(Profile("dm-self"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = alice.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("self_conversation", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DirectConversation_WithUnknownUser_Returns404()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("dm-unknown"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", cookie, new { userId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DirectConversations_AreVisibleOnlyToTheirParticipants()
    {
        var (alice, aliceCookie) = await factory.CreateSessionAsync(Profile("dm-list-alice"));
        var (bob, _) = await factory.CreateSessionAsync(Profile("dm-list-bob"));
        var (_, strangerCookie) = await factory.CreateSessionAsync(Profile("dm-list-stranger"));
        using var client = factory.CreateApiClient();
        var created = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Post("/api/direct-conversations", aliceCookie, new { userId = bob.Id, text = "hi" })));
        var conversationId = created.GetProperty("id").GetGuid();

        var aliceList = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-conversations", aliceCookie)));
        var strangerList = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/direct-conversations", strangerCookie)));

        Assert.Contains(aliceList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == conversationId);
        Assert.DoesNotContain(strangerList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == conversationId);
    }

    [Fact]
    public async Task UserSearch_TooShortQuery_Returns400WithCode()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("search-short"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Get("/api/users/search?query=a", cookie));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("query_too_short", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task UserSearch_FindsOthers_ExcludesSelf_AndNeverReturnsEmail()
    {
        var token = Guid.NewGuid().ToString("N");
        var (me, meCookie) = await factory.CreateSessionAsync(new GoogleProfile($"search-me-{token}", $"me-{token}@example.com", $"Zq{token} Me", null));
        await factory.CreateSessionAsync(new GoogleProfile($"search-other-{token}", $"other-{token}@example.com", $"Zq{token} Other", null));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Get($"/api/users/search?query=zq{token}", meCookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var users = await ChatHttp.ReadJsonAsync(response);
        var names = users.EnumerateArray().Select(user => user.GetProperty("displayName").GetString()).ToList();
        Assert.Single(names);
        Assert.Equal($"Zq{token} Other", names[0]);
        Assert.DoesNotContain(me.Id.ToString(), raw);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@example.com", raw);
    }

    private static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", null);
}

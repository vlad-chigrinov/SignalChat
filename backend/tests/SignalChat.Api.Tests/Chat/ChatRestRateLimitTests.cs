using System.Net;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Each limited REST route answers 429 past its allowance, per user, with Retry-After and the shared code.</summary>
public sealed class ChatRestRateLimitTests
{
    [Fact]
    public async Task UserSearch_IsLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-search"));
        using var client = factory.CreateApiClient();

        await AssertLimitedAfterPermitsAsync(
            HttpStatusCode.OK, () => client.SendAsync(ChatHttp.Get("/api/users/search?query=rl", cookie)));
    }

    [Fact]
    public async Task RoomDiscover_IsLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-discover"));
        using var client = factory.CreateApiClient();

        await AssertLimitedAfterPermitsAsync(
            HttpStatusCode.OK, () => client.SendAsync(ChatHttp.Get("/api/rooms/discover", cookie)));
    }

    [Fact]
    public async Task RoomCreate_IsLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-create"));
        using var client = factory.CreateApiClient();

        await AssertLimitedAfterPermitsAsync(
            HttpStatusCode.Created,
            () => client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = ChatHttp.UniqueName("RL"), description = "" })));
    }

    [Fact]
    public async Task DirectConversationCreate_IsLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-direct"));
        var (peer, _) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-direct-peer"));
        using var client = factory.CreateApiClient();

        // The first request is created, the repeat is answered with dm_request_pending: both count, the third is refused.
        using var created = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", cookie, new { userId = peer.Id, text = "hi" }));
        using var repeated = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", cookie, new { userId = peer.Id, text = "hi" }));
        using var limited = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", cookie, new { userId = peer.Id, text = "hi" }));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        await AssertRefusedAsRateLimitedAsync(limited);
    }

    [Fact]
    public async Task JoinAndLeave_ShareOneLimit()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-membership"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);

        using var joined = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", cookie));
        using var left = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", cookie));
        using var limited = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", cookie));

        Assert.Equal(HttpStatusCode.NoContent, joined.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        await AssertRefusedAsRateLimitedAsync(limited);
    }

    [Fact]
    public async Task Reactions_AreLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-react"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await ReactionFixtures.AppendMessageAsync(factory, owner.Id, roomId);

        await AssertLimitedAfterPermitsAsync(
            HttpStatusCode.OK, () => ReactionFixtures.ReactAsync(client, cookie, roomId, messageId, ReactionFixtures.ThumbsUp));
    }

    [Fact]
    public async Task RoomInvite_IsLimitedPerMinute()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-invite"));
        var (stranger, _) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-invite-peer"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);

        // Refused invitations count too: the limit protects the route, not only the successes.
        await AssertLimitedAfterPermitsAsync(
            HttpStatusCode.NotFound,
            () => client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/invitations", cookie, new { userId = stranger.Id })));
    }

    [Fact]
    public async Task AnExhaustedUser_DoesNotBlockAnotherUser()
    {
        using var factory = new LowRateLimitApiFactory();
        var (_, firstCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-first"));
        var (_, secondCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rl-second"));
        using var client = factory.CreateApiClient();
        for (var call = 0; call < LowRateLimitApiFactory.TightRestPermitsPerMinute; call++)
        {
            using var allowed = await client.SendAsync(ChatHttp.Get("/api/rooms/discover", firstCookie));
        }

        using var limited = await client.SendAsync(ChatHttp.Get("/api/rooms/discover", firstCookie));
        using var otherUser = await client.SendAsync(ChatHttp.Get("/api/rooms/discover", secondCookie));

        await AssertRefusedAsRateLimitedAsync(limited);
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }

    /// <summary>Sends the allowed number of calls, expecting <paramref name="allowedStatus"/>, then one more that must be refused.</summary>
    private static async Task AssertLimitedAfterPermitsAsync(HttpStatusCode allowedStatus, Func<Task<HttpResponseMessage>> call)
    {
        for (var index = 0; index < LowRateLimitApiFactory.TightRestPermitsPerMinute; index++)
        {
            using var allowed = await call();
            Assert.Equal(allowedStatus, allowed.StatusCode);
        }

        using var limited = await call();
        await AssertRefusedAsRateLimitedAsync(limited);
    }

    private static async Task AssertRefusedAsRateLimitedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.True(response.Headers.RetryAfter.Delta > TimeSpan.Zero);
        var body = await ChatHttp.ReadJsonAsync(response);
        Assert.Equal(HubErrorCodes.RateLimited, body.GetProperty("code").GetString());
    }
}

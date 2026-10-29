using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Chat;

public sealed class RoomsRestTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task RoomEndpoints_WithoutSession_Return401()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_Returns201_AndTheCallerIsTheOnlyMember()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("create"));
        using var client = factory.CreateApiClient();
        var name = ChatHttp.UniqueName("Create");

        var created = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name, description = "About" }));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ChatHttp.ReadJsonAsync(created);
        Assert.Equal("room", body.GetProperty("kind").GetString());
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(1, body.GetProperty("memberCount").GetInt32());
        var mine = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/rooms", cookie)));
        Assert.Contains(mine.EnumerateArray(), room => room.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task CreateRoom_DuplicateNameIgnoringCase_Returns409WithCode()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("dup"));
        using var client = factory.CreateApiClient();
        var name = ChatHttp.UniqueName("Dup");
        await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name, description = "" }));

        var response = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = name.ToUpperInvariant(), description = "" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_name_taken", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateRoom_TooShortName_Returns400WithCode()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("short"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = "ab", description = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_room_name", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateRoom_OverTheOwnedLimit_Returns409WithCode()
    {
        using var limitFactory = new SignalChatApiFactory();
        var (_, cookie) = await limitFactory.CreateSessionAsync(Profile("limit"));
        using var client = limitFactory.CreateApiClient();
        for (var created = 0; created < Room.MaxOwnedRoomsPerUser; created++)
        {
            var ok = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = ChatHttp.UniqueName("Lim"), description = "" }));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var response = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = ChatHttp.UniqueName("Over"), description = "" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_limit_reached", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Join_IsIdempotent_AndLeaveByNonMemberIs404()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(Profile("join-owner"));
        var (_, guestCookie) = await factory.CreateSessionAsync(Profile("join-guest"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);

        var first = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", guestCookie));
        var second = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", guestCookie));
        var stranger = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", (await factory.CreateSessionAsync(Profile("join-stranger"))).Cookie));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task Owner_CannotLeave_WhileOthersRemain_ButMembersCan()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(Profile("owner-leave"));
        var (_, memberCookie) = await factory.CreateSessionAsync(Profile("member-leave"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", memberCookie));

        var ownerLeave = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", ownerCookie));
        var memberLeave = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", memberCookie));
        var memberLeaveAgain = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", memberCookie));

        Assert.Equal(HttpStatusCode.Conflict, ownerLeave.StatusCode);
        Assert.Equal("owner_cannot_leave", (await ChatHttp.ReadJsonAsync(ownerLeave)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, memberLeave.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, memberLeaveAgain.StatusCode);
    }

    [Fact]
    public async Task JoinMissingOrMalformedRoom_Returns404()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("join-missing"));
        using var client = factory.CreateApiClient();

        var missing = await client.SendAsync(ChatHttp.Post($"/api/rooms/{Guid.NewGuid()}/join", cookie));
        var malformed = await client.SendAsync(ChatHttp.Post("/api/rooms/not-a-guid/join", cookie));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task MyRooms_ShowOnlyMyMemberships_AndDiscoverHidesJoinedRoomsAndContent()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(Profile("visibility-owner"));
        var (_, outsiderCookie) = await factory.CreateSessionAsync(Profile("visibility-outsider"));
        using var client = factory.CreateApiClient();
        var name = ChatHttp.UniqueName("Visible");
        var roomId = await CreateRoomAsync(client, ownerCookie, name);
        await AppendAsync(roomId, await UserIdAsync(ownerCookie), "secret content in room");

        var outsiderRooms = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/rooms", outsiderCookie)));
        var discovered = await ChatHttp.ReadJsonAsync(
            await client.SendAsync(ChatHttp.Get($"/api/rooms/discover?query={Uri.EscapeDataString(name)}", outsiderCookie)));

        Assert.DoesNotContain(outsiderRooms.EnumerateArray(), room => room.GetProperty("id").GetGuid() == roomId);
        var found = Assert.Single(discovered.EnumerateArray());
        Assert.Equal(roomId, found.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, found.GetProperty("lastMessage").ValueKind);
        Assert.DoesNotContain("secret content", discovered.GetRawText());
    }

    [Fact]
    public async Task MyRooms_ShowTheLastMessage_ToMembers()
    {
        var (owner, ownerCookie) = await factory.CreateSessionAsync(Profile("last-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await CreateRoomAsync(client, ownerCookie);
        await AppendAsync(roomId, owner.Id, "the newest words");

        var mine = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/rooms", ownerCookie)));

        var room = mine.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == roomId);
        Assert.Equal("the newest words", room.GetProperty("lastMessage").GetProperty("text").GetString());
    }

    private async Task<Guid> CreateRoomAsync(HttpClient client, string cookie, string? name = null)
    {
        var response = await client.SendAsync(ChatHttp.Post("/api/rooms", cookie, new { name = name ?? ChatHttp.UniqueName("Room"), description = "" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ChatHttp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> UserIdAsync(string cookie)
    {
        using var client = factory.CreateApiClient();
        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        return me.GetProperty("id").GetGuid();
    }

    private async Task AppendAsync(Guid roomId, Guid authorId, string text)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();
        await store.AppendAsync(authorId, roomId, text, CancellationToken.None);
    }

    private static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", null);
}

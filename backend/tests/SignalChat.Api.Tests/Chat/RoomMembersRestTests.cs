using System.Net;
using System.Text.Json;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Chat;

/// <summary>The member list of a room: members only, the current public profile, no email.</summary>
public sealed class RoomMembersRestTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Members_WithoutSession_Return401()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Members_ForAMember_ListTheOwnerFirstWithNamesAvatarsAndRoles()
    {
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-owner"));
        var (member, memberCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-member"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, memberCookie, roomId);

        var response = await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", memberCookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = (await ChatHttp.ReadJsonAsync(response)).EnumerateArray().ToList();
        Assert.Equal(2, members.Count);
        Assert.Equal(owner.Id, members[0].GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("owner", members[0].GetProperty("role").GetString());
        Assert.Equal(member.Id, members[1].GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("member", members[1].GetProperty("role").GetString());
        Assert.Equal(member.DisplayName, members[1].GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, members[1].GetProperty("user").GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Members_NeverCarryAnEmail()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-mail-owner"));
        var (_, memberCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-mail-member"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, memberCookie, roomId);

        var response = await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", ownerCookie));

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Members_ShowTheNameTheUserHasNow()
    {
        var (member, memberCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-rename"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, memberCookie);
        (await client.SendAsync(ChatHttp.Put("/api/me/display-name", memberCookie, new { displayName = "Renamed Member" })))
            .EnsureSuccessStatusCode();

        var response = await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", memberCookie));

        var listed = (await ChatHttp.ReadJsonAsync(response)).EnumerateArray().Single();
        Assert.Equal(member.Id, listed.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("Renamed Member", listed.GetProperty("user").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Members_ForANonMember_Return404NotFound_ThatLooksLikeAMissingRoom()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-hidden-owner"));
        var (_, outsiderCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-outsider"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);

        var notMember = await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", outsiderCookie));
        var missing = await client.SendAsync(ChatHttp.Get($"/api/rooms/{Guid.NewGuid()}/members", outsiderCookie));

        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not_found", (await ChatHttp.ReadJsonAsync(notMember)).GetProperty("code").GetString());
        Assert.Equal("not_found", (await ChatHttp.ReadJsonAsync(missing)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Members_AfterLeavingTheRoom_Return404()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-left-owner"));
        var (_, leaverCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("mem-leaver"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, leaverCookie, roomId);
        await HubFixtures.LeaveRoomOverRestAsync(client, leaverCookie, roomId);

        var response = await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", leaverCookie));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

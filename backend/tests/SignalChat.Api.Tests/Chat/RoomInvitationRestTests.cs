using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Inviting into a room: a member invites a chat partner, and nobody else.</summary>
public sealed class RoomInvitationRestTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Invite_WithoutSession_Returns401()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/invitations", new { userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invite_OfAChatPartner_AddsThemToTheRoomWithoutAConfirmation()
    {
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);

        var response = await Invite(client, ownerCookie, roomId, friend.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rooms = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/rooms", friendCookie)));
        var invited = rooms.EnumerateArray().Single(room => room.GetProperty("id").GetGuid() == roomId);
        Assert.Equal(2, invited.GetProperty("memberCount").GetInt32());
        var members = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get($"/api/rooms/{roomId}/members", friendCookie)));
        Assert.Contains(members.EnumerateArray(), member => member.GetProperty("user").GetProperty("id").GetGuid() == owner.Id);
        Assert.Equal(2, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_ByAnyMember_NotOnlyTheOwner()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-any-owner"));
        var (member, memberCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-any-member"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-any-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, memberCookie, roomId);
        await DirectRequestFixtures.BefriendAsync(client, memberCookie, friendCookie, friend.Id);

        var response = await Invite(client, memberCookie, roomId, friend.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(3, await MemberCountAsync(roomId));
        Assert.NotEqual(member.Id, friend.Id);
    }

    [Fact]
    public async Task Invite_ByANonMember_IsRefusedAsNotFound_AndAddsNobody()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-nm-owner"));
        var (_, outsiderCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-nm-outsider"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-nm-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, outsiderCookie, friendCookie, friend.Id);

        var response = await Invite(client, outsiderCookie, roomId, friend.Id);

        await AssertNotFoundAsync(response);
        Assert.Equal(1, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_OfSomeoneWithoutAChat_IsRefusedAsNotFound()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-st-owner"));
        var (stranger, _) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-stranger"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);

        var response = await Invite(client, ownerCookie, roomId, stranger.Id);

        await AssertNotFoundAsync(response);
        Assert.Equal(1, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_OfSomeoneWhoSharesOnlyAnotherRoom_IsRefused()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-sr-owner"));
        var (mate, mateCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-sr-mate"));
        using var client = factory.CreateApiClient();
        var firstRoom = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        var secondRoom = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await HubFixtures.JoinRoomAsync(client, mateCookie, firstRoom);

        var response = await Invite(client, ownerCookie, secondRoom, mate.Id);

        await AssertNotFoundAsync(response);
        Assert.Equal(1, await MemberCountAsync(secondRoom));
    }

    [Fact]
    public async Task Invite_WhileTheChatIsOnlyARequest_IsRefused_InBothDirections()
    {
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-pend-owner"));
        var (asked, _) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-pend-asked"));
        var (asker, askerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-pend-asker"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.RequestAsync(client, ownerCookie, asked.Id, "Hello");
        await DirectRequestFixtures.RequestAsync(client, askerCookie, owner.Id, "Hello you");

        var toTheOneAsked = await Invite(client, ownerCookie, roomId, asked.Id);
        var toTheOneWhoAsked = await Invite(client, ownerCookie, roomId, asker.Id);

        await AssertNotFoundAsync(toTheOneAsked);
        await AssertNotFoundAsync(toTheOneWhoAsked);
        Assert.Equal(1, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_AfterTheRequestWasDeclined_IsRefused()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-dec-owner"));
        var (declining, decliningCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-decliner"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        var conversationId = await DirectRequestFixtures.RequestAsync(client, ownerCookie, declining.Id, "Hello");
        await DirectRequestFixtures.DeclineAsync(client, decliningCookie, conversationId);

        var response = await Invite(client, ownerCookie, roomId, declining.Id);

        await AssertNotFoundAsync(response);
    }

    [Fact]
    public async Task Invite_OfAnIdThatIsNoUser_LooksLikeAStranger()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-ghost-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);

        var unknownUser = await Invite(client, ownerCookie, roomId, Guid.NewGuid());
        var emptyId = await Invite(client, ownerCookie, roomId, Guid.Empty);

        await AssertNotFoundAsync(unknownUser);
        await AssertNotFoundAsync(emptyId);
    }

    [Fact]
    public async Task Invite_IntoAMissingRoom_IsRefusedAsNotFound()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-noroom-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-noroom-friend"));
        using var client = factory.CreateApiClient();
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);

        var response = await Invite(client, ownerCookie, Guid.NewGuid(), friend.Id);

        await AssertNotFoundAsync(response);
    }

    [Fact]
    public async Task Invite_OfSomeoneAlreadyInTheRoom_Returns409AlreadyMember_AndMakesNoDuplicate()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-dup-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-dup-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await HubFixtures.JoinRoomAsync(client, friendCookie, roomId);

        var response = await Invite(client, ownerCookie, roomId, friend.Id);
        var again = await Invite(client, ownerCookie, roomId, friend.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RoomMemberErrorCodes.AlreadyMember, (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(2, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_TwiceInARow_SecondIsAlreadyMember()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-twice-owner"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-twice-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);

        var first = await Invite(client, ownerCookie, roomId, friend.Id);
        var second = await Invite(client, ownerCookie, roomId, friend.Id);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(2, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task Invite_OfOneself_IsRefusedAsNotFound()
    {
        var (owner, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-self-owner"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);

        var response = await Invite(client, ownerCookie, roomId, owner.Id);

        await AssertNotFoundAsync(response);
        Assert.Equal(1, await MemberCountAsync(roomId));
    }

    [Fact]
    public async Task InvitedUser_CanReadTheHistory_AndANonInvitedChatPartnerCannot()
    {
        var (_, ownerCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-hist-owner"));
        var (invited, invitedCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-hist-invited"));
        var (friend, friendCookie) = await factory.CreateSessionAsync(HubFixtures.Profile("inv-hist-friend"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, ownerCookie);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, invitedCookie, invited.Id);
        await DirectRequestFixtures.BefriendAsync(client, ownerCookie, friendCookie, friend.Id);
        await Invite(client, ownerCookie, roomId, invited.Id);

        var allowed = await client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages", invitedCookie));
        var denied = await client.SendAsync(ChatHttp.Get($"/api/conversations/{roomId}/messages", friendCookie));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    private static Task<HttpResponseMessage> Invite(HttpClient client, string cookie, Guid roomId, Guid userId) =>
        client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/invitations", cookie, new { userId }));

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    private async Task<int> MemberCountAsync(Guid roomId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        return await db.RoomMembers.CountAsync(member => member.RoomId == roomId);
    }
}

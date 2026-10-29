using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using HubException = Microsoft.AspNetCore.SignalR.HubException;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>JoinRoom and LeaveRoom share one limit per user, counted across all of the user's connections.</summary>
public sealed class ChatHubMembershipRateLimitTests
{
    [Fact]
    public async Task JoinAndLeave_ShareOneWindow_AndTheCallAboveTheLimitIsRateLimited()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("membership-rate"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);

        for (var call = 0; call < ChatHubLimits.MembershipChangePermits; call++)
        {
            await connection.InvokeAsync("JoinRoom", roomId.ToString());
        }
        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("LeaveRoom", roomId.ToString()));

        Assert.Equal(HubErrorCodes.RateLimited, HubTestErrors.CodeOf(error));
    }

    [Fact]
    public async Task TheLimitIsPerUser_AcrossConnections()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("membership-rate-two-tabs"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var firstTab = await HubTestClient.ConnectAsync(factory, cookie);
        await using var secondTab = await HubTestClient.ConnectAsync(factory, cookie);
        for (var call = 0; call < ChatHubLimits.MembershipChangePermits; call++)
        {
            await firstTab.InvokeAsync("JoinRoom", roomId.ToString());
        }

        var error = await Assert.ThrowsAsync<HubException>(() => secondTab.InvokeAsync("JoinRoom", roomId.ToString()));

        Assert.Equal(HubErrorCodes.RateLimited, HubTestErrors.CodeOf(error));
    }

    [Fact]
    public async Task AfterTheWindowPasses_TheUserMayJoinAgain()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("membership-rate-window"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        await using var connection = await HubTestClient.ConnectAsync(factory, cookie);
        for (var call = 0; call < ChatHubLimits.MembershipChangePermits; call++)
        {
            await connection.InvokeAsync("JoinRoom", roomId.ToString());
        }

        factory.Clock.Advance(ChatHubLimits.MembershipChangeWindow);

        await connection.InvokeAsync("LeaveRoom", roomId.ToString());
    }
}

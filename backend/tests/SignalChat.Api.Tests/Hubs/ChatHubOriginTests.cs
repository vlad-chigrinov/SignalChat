using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>The WebSocket handshake carries Origin: the allowlist applies to the upgrade, not only to REST.</summary>
public sealed class ChatHubOriginTests
{
    [Fact]
    public async Task AllowedOrigin_Connects()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("origin-ok"));

        await using var connection = HubTestClient.Create(factory, cookie, SignalChatApiFactory.AllowedOrigin);
        await connection.StartAsync();

        Assert.Equal(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task ForeignOrigin_IsRefused()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("origin-evil"));
        await using var connection = HubTestClient.Create(factory, cookie, SignalChatApiFactory.ForeignOrigin);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task NoOrigin_IsAcceptedAsANonBrowserClient()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("origin-none"));
        await using var connection = HubTestClient.Create(factory, cookie, origin: null);

        await connection.StartAsync();

        Assert.Equal(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected, connection.State);
    }
}

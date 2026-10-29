using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using Xunit;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>A user may hold a fixed number of hub connections. The one over the limit is refused, the open ones stay.</summary>
public sealed class ChatHubConnectionLimitTests
{
    [Fact]
    public async Task TheConnectionOverTheLimit_IsRefused_AndTheOpenOnesStay()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("connection-limit"));
        var open = new List<HubConnection>();
        try
        {
            for (var index = 0; index < ChatHubLimits.MaxConnectionsPerUser; index++)
            {
                open.Add(await HubTestClient.ConnectAsync(factory, cookie));
            }

            await using var refused = HubTestClient.Create(factory, cookie);
            // The refusal may surface during the handshake or right after it; the outcome is checked below.
            _ = await Record.ExceptionAsync(() => refused.StartAsync());

            await Eventually.UntilAsync(
                () => refused.State == HubConnectionState.Disconnected, "the refused connection to close");
            Assert.All(open, connection => Assert.Equal(HubConnectionState.Connected, connection.State));
        }
        finally
        {
            foreach (var connection in open)
            {
                await connection.DisposeAsync();
            }
        }
    }
}

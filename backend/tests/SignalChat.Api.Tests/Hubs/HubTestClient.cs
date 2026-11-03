using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>A SignalR client wired into the in-memory TestServer, with the session cookie and Origin headers.</summary>
internal static class HubTestClient
{
    public static HubConnection Create(
        SignalChatApiFactory factory, string? cookie, string? origin = SignalChatApiFactory.AllowedOrigin)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(SignalChatApiFactory.ApiBaseAddress, ChatHub.Route), options =>
            {
                if (cookie is not null)
                {
                    options.Headers["Cookie"] = cookie;
                }
                if (origin is not null)
                {
                    options.Headers["Origin"] = origin;
                }
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.WebSocketFactory = (context, token) =>
                    new ValueTask<WebSocket>(factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, token));
            })
            .Build();
    }

    public static async Task<HubConnection> ConnectAsync(SignalChatApiFactory factory, string cookie)
    {
        var connection = Create(factory, cookie);
        await connection.StartAsync();
        await WaitForOnConnectedAsync(connection);
        return connection;
    }

    /// <summary>
    /// StartAsync returns after the handshake, which the server answers before OnConnectedAsync has run, so the user
    /// may not be in the personal group yet. The server dispatches client calls only after OnConnectedAsync, so the
    /// answer to a call of an unknown method (an error, with no side effect) proves it has finished.
    /// </summary>
    private static async Task WaitForOnConnectedAsync(HubConnection connection)
    {
        try
        {
            await connection.InvokeAsync(UnknownHubMethod);
        }
        catch (HubException)
        {
            // Expected: the method does not exist.
        }
    }

    private const string UnknownHubMethod = "WaitForOnConnected";
}

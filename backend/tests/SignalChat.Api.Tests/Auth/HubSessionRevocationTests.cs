using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

public sealed class HubSessionRevocationTests
{
    private static readonly TimeSpan ClosedWithin = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Logout_ClosesTheLiveHubConnectionsOfTheUser()
    {
        using var factory = new SignalChatApiFactory();
        var (_, cookie) = await factory.CreateSessionAsync();
        await using var connection = BuildConnection(factory, cookie);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        await connection.StartAsync();
        using var client = factory.CreateApiClient();

        var logout = await client.SendAsync(LogoutRequest(cookie));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await closed.Task.WaitAsync(ClosedWithin);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    private static HubConnection BuildConnection(SignalChatApiFactory factory, string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(SignalChatApiFactory.ApiBaseAddress, ChatHub.Route), options =>
            {
                options.Headers["Cookie"] = cookie;
                options.Headers["Origin"] = SignalChatApiFactory.AllowedOrigin;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.WebSocketFactory = (context, token) =>
                    new ValueTask<WebSocket>(factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, token));
            })
            .Build();

    private static HttpRequestMessage LogoutRequest(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", SignalChatApiFactory.AllowedOrigin);
        return request;
    }
}

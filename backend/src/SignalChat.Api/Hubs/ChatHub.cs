using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Real-time chat hub (SignalR, WebSocket over WSS). Authenticated by the same session cookie as the
/// REST API; <c>Context.UserIdentifier</c> is the internal user id. Connections are tracked so that a
/// logout can cut them off. Room/DM methods and per-room access checks arrive with ticket #6.
/// </summary>
[Authorize]
public sealed class ChatHub(IHubConnectionRegistry connections) : Hub
{
    public const string Route = "/hubs/chat";

    public override Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        connections.Register(userId, Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            connections.Unregister(userId, Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}

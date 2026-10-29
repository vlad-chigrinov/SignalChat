using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Real-time chat hub (SignalR, WebSocket over WSS). Authenticated by the same session cookie as the
/// REST API; <c>Context.UserIdentifier</c> is the internal user id. Room/DM methods and per-room
/// access checks arrive with ticket #6.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    public const string Route = "/hubs/chat";
}

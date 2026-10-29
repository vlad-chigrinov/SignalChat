using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>What one hub connection remembers, kept in <c>HubCallerContext.Items</c> (no database read needed).</summary>
public static class ChatConnectionState
{
    private const string JoinedKey = "chat.joined";
    private const string PresenceOpenedKey = "chat.presenceOpened";

    /// <summary>
    /// Stores the joined map of this connection. The same instance is held by the connection registry, so a REST
    /// leave can clear entries from another thread; the map is thread-safe for that reason.
    /// </summary>
    public static void Attach(HubCallerContext context, ConcurrentDictionary<Guid, ConversationRef> joined) =>
        context.Items[JoinedKey] = joined;

    /// <summary>The conversations this connection joined. Only these may receive typing signals.</summary>
    public static ConcurrentDictionary<Guid, ConversationRef> Joined(HubCallerContext context) =>
        (ConcurrentDictionary<Guid, ConversationRef>)context.Items[JoinedKey]!;

    public static void MarkPresenceOpened(HubCallerContext context) => context.Items[PresenceOpenedKey] = true;

    /// <summary>True once, when this connection had counted itself as online; the flag is consumed.</summary>
    public static bool TakePresenceOpened(HubCallerContext context) => context.Items.Remove(PresenceOpenedKey);
}

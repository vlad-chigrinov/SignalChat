using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>What one hub connection remembers, kept in <c>HubCallerContext.Items</c> (no database read needed).</summary>
public static class ChatConnectionState
{
    private const string JoinedKey = "chat.joined";
    private const string PresenceOpenedKey = "chat.presenceOpened";

    /// <summary>The conversations this connection joined. Only these may receive typing signals.</summary>
    public static Dictionary<Guid, ConversationRef> Joined(HubCallerContext context)
    {
        if (context.Items.TryGetValue(JoinedKey, out var existing) && existing is Dictionary<Guid, ConversationRef> joined)
        {
            return joined;
        }

        var created = new Dictionary<Guid, ConversationRef>();
        context.Items[JoinedKey] = created;
        return created;
    }

    public static void MarkPresenceOpened(HubCallerContext context) => context.Items[PresenceOpenedKey] = true;

    /// <summary>True once, when this connection had counted itself as online; the flag is consumed.</summary>
    public static bool TakePresenceOpened(HubCallerContext context) => context.Items.Remove(PresenceOpenedKey);
}

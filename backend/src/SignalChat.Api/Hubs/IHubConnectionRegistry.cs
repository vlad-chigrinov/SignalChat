using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Live hub connections by user, so a revoked session can be cut off in real time and a REST leave can reach
/// the connections' group membership and joined state. <c>userId</c> is <c>Context.UserIdentifier</c>.
/// </summary>
public interface IHubConnectionRegistry
{
    void Register(string userId, HubCallerContext connection, ConcurrentDictionary<Guid, ConversationRef> joined);

    void Unregister(string userId, string connectionId);

    /// <summary>Aborts every live connection of the user. Connections opened afterwards are not affected.</summary>
    void AbortAll(string userId);

    /// <summary>The user's open connections; empty when none. A snapshot: safe to use after the call returns.</summary>
    IReadOnlyCollection<HubConnectionEntry> ConnectionsOf(string userId);
}

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// One live connection as the registry sees it. <see cref="Joined"/> is the same map the hub uses for this
/// connection; REST leave removes the conversation from it, so the connection cannot skip a later join.
/// </summary>
public sealed record HubConnectionEntry(
    string ConnectionId,
    HubCallerContext Context,
    ConcurrentDictionary<Guid, ConversationRef> Joined);

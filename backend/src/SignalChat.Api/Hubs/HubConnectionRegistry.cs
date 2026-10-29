using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Process-wide registry (singleton). Every mutation runs under one lock, so a user's inner map is never
/// dropped while a connection is being added to it. The lock is held only for dictionary work; the
/// connections are aborted outside it.
/// </summary>
public sealed class HubConnectionRegistry : IHubConnectionRegistry
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HubConnectionEntry>> _connectionsByUser =
        new(StringComparer.Ordinal);

    public void Register(string userId, HubCallerContext connection, ConcurrentDictionary<Guid, ConversationRef> joined)
    {
        lock (_gate)
        {
            var connections = _connectionsByUser.GetOrAdd(
                userId, _ => new ConcurrentDictionary<string, HubConnectionEntry>(StringComparer.Ordinal));
            connections[connection.ConnectionId] = new HubConnectionEntry(connection.ConnectionId, connection, joined);
        }
    }

    public void Unregister(string userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionsByUser.TryGetValue(userId, out var connections))
            {
                return;
            }

            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
            {
                _connectionsByUser.TryRemove(userId, out _);
            }
        }
    }

    public IReadOnlyCollection<HubConnectionEntry> ConnectionsOf(string userId)
    {
        lock (_gate)
        {
            return _connectionsByUser.TryGetValue(userId, out var connections)
                ? connections.Values.ToArray()
                : [];
        }
    }

    public void AbortAll(string userId)
    {
        HubConnectionEntry[] victims;
        lock (_gate)
        {
            if (!_connectionsByUser.TryRemove(userId, out var connections))
            {
                return;
            }
            victims = connections.Values.ToArray();
        }

        foreach (var connection in victims)
        {
            connection.Context.Abort();
        }
    }
}

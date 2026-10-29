using Microsoft.AspNetCore.SignalR;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Tells the users who share a room or a direct conversation with someone that the someone went online or
/// offline. Singleton: it opens its own scope to ask the scoped <see cref="IPeerDirectory"/>.
/// </summary>
public sealed class PresenceNotifier(IHubContext<ChatHub, IChatClient> hub, IServiceScopeFactory scopes)
{
    public async Task NotifyAsync(Guid userId, bool isOnline, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> peerIds;
        using (var scope = scopes.CreateScope())
        {
            var peers = scope.ServiceProvider.GetRequiredService<IPeerDirectory>();
            peerIds = await peers.GetPeerIdsAsync(userId, cancellationToken);
        }

        if (peerIds.Count == 0)
        {
            return;
        }

        var peerGroups = peerIds.Select(ChatGroups.User).ToList();
        await hub.Clients.Groups(peerGroups).PresenceChanged(new PresenceChangedDto(userId, isOnline));
    }
}

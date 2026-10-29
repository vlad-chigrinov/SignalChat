using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Hubs;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

/// <summary>Presence snapshot: only users who share a room or a direct conversation with the caller.</summary>
public static class PresenceEndpoints
{
    public static RouteGroupBuilder MapPresenceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/presence/online", OnlinePeersAsync);
        return api;
    }

    private static async Task<IResult> OnlinePeersAsync(
        ClaimsPrincipal user, IPeerDirectory peers, IPresenceTracker presence, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var peerIds = await peers.GetPeerIdsAsync(userId, cancellationToken);
        return Results.Ok(peerIds.Where(presence.IsOnline).ToList());
    }
}

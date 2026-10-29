using Microsoft.AspNetCore.SignalR;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Live hub connections by user, so a revoked session can be cut off in real time.
/// <c>userId</c> is <c>Context.UserIdentifier</c> (the internal user id).
/// </summary>
public interface IHubConnectionRegistry
{
    void Register(string userId, HubCallerContext connection);

    void Unregister(string userId, string connectionId);

    /// <summary>Aborts every live connection of the user. Connections opened afterwards are not affected.</summary>
    void AbortAll(string userId);
}

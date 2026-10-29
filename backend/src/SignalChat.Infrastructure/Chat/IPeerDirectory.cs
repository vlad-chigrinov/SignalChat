namespace SignalChat.Infrastructure.Chat;

/// <summary>Users who share a room or a direct conversation with a given user. Presence (#6) is scoped to them.</summary>
public interface IPeerDirectory
{
    Task<IReadOnlyList<Guid>> GetPeerIdsAsync(Guid userId, CancellationToken cancellationToken);
}

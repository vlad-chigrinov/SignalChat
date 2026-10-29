namespace SignalChat.Infrastructure.Chat;

public interface IRoomService
{
    /// <summary>Creates a room and makes the caller its owner. Throws ChatRuleException on rule violations.</summary>
    Task<RoomSummary> CreateAsync(Guid userId, string? name, string? description, CancellationToken cancellationToken);

    /// <summary>Rooms the user is a member of, most recently active first.</summary>
    Task<IReadOnlyList<RoomSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Rooms the user has not joined yet, filtered by name. No message content is exposed.</summary>
    Task<IReadOnlyList<RoomSummary>> DiscoverAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken);

    /// <summary>Idempotent. Throws "not_found" for a missing room.</summary>
    Task JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);

    /// <summary>Throws "not_found" for a non-member and "owner_cannot_leave" while other members remain.</summary>
    Task LeaveAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);
}

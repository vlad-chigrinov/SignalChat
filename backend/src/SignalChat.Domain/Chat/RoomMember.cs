namespace SignalChat.Domain.Chat;

/// <summary>A user's membership of a room. The pair (RoomId, UserId) is the key.</summary>
public sealed class RoomMember
{
    private RoomMember()
    {
        // Required by EF Core.
    }

    public Guid RoomId { get; private set; }

    public Guid UserId { get; private set; }

    public RoomRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public bool IsOwner => Role == RoomRole.Owner;

    public static RoomMember Join(Guid roomId, Guid userId, RoomRole role, DateTimeOffset now) =>
        new()
        {
            RoomId = roomId,
            UserId = userId,
            Role = role,
            JoinedAt = now,
        };
}

namespace SignalChat.Infrastructure.Chat;

/// <summary>Who is in a room, and adding people to it by invitation.</summary>
public interface IRoomMemberService
{
    /// <summary>
    /// The members of a room the caller belongs to: the owner first, then by name. Throws "not_found" for a missing
    /// room and for a caller who is not a member (the same answer).
    /// </summary>
    Task<IReadOnlyList<RoomMemberView>> ListAsync(Guid userId, Guid roomId, int limit, CancellationToken cancellationToken);

    /// <summary>The ids of every member of the room; used to address live events.</summary>
    Task<IReadOnlyList<Guid>> ListMemberIdsAsync(Guid roomId, CancellationToken cancellationToken);

    /// <summary>
    /// A member adds a chat partner (an accepted direct conversation) to the room, with no confirmation. Throws
    /// "not_found" when the room is missing, the inviter is not a member, or the two have no accepted chat (an unknown
    /// user id gets the same answer), and "already_member" when the invitee is in the room already.
    /// </summary>
    Task<RoomMemberAdded> InviteAsync(Guid inviterId, Guid roomId, Guid inviteeId, CancellationToken cancellationToken);
}

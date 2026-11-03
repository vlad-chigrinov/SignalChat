namespace SignalChat.Domain.Chat;

/// <summary>Stable error codes of room membership changes. REST returns them in ProblemDetails.</summary>
public static class RoomMemberErrorCodes
{
    /// <summary>The invited user is already a member of the room: nothing was changed.</summary>
    public const string AlreadyMember = "already_member";
}

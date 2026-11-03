namespace SignalChat.Api.Chat;

/// <summary>Page sizes of the list endpoints (message history uses ChatPaging in Infrastructure).</summary>
public static class ChatLimits
{
    public const int DiscoverDefaultLimit = 20;
    public const int DiscoverMaxLimit = 50;
    /// <summary>The member list of one room is cut here; the room's member count stays the real number.</summary>
    public const int RoomMembersMaxCount = 500;
    public const int UserSearchDefaultLimit = 10;
    public const int UserSearchMaxLimit = 20;
}

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>
/// REST requests one user may make per minute on each limited route. Bound from <c>Chat:RateLimits</c>, so a
/// deployment can change a window without a rebuild. A missing key keeps its default.
/// </summary>
public sealed class ChatRateLimitOptions
{
    public const string SectionName = "Chat:RateLimits";

    public int UserSearchPerMinute { get; set; } = 30;

    public int RoomDiscoverPerMinute { get; set; } = 30;

    /// <summary>Join and leave share this one limit.</summary>
    public int RoomMembershipPerMinute { get; set; } = 30;

    /// <summary>Invitations into rooms; refused ones count too.</summary>
    public int RoomInvitePerMinute { get; set; } = 20;

    public int DirectConversationCreatePerMinute { get; set; } = 10;

    public int RoomCreatePerMinute { get; set; } = 10;

    /// <summary>Name and avatar changes share this limit; an upload costs a decode, so it stays low.</summary>
    public int ProfileUpdatePerMinute { get; set; } = 10;

    /// <summary>Picking, replacing and removing a reaction share this limit; each is a database write and a broadcast.</summary>
    public int ReactionPerMinute { get; set; } = 60;

    /// <summary>Requests per client address; the test sign-in is not part of any user's allowance.</summary>
    public int TestLoginPerMinute { get; set; } = 20;

    /// <summary>The names of the settings above, so a test can override every one of them by name.</summary>
    public static string[] SettingNames =>
    [
        nameof(UserSearchPerMinute),
        nameof(RoomDiscoverPerMinute),
        nameof(RoomMembershipPerMinute),
        nameof(RoomInvitePerMinute),
        nameof(DirectConversationCreatePerMinute),
        nameof(RoomCreatePerMinute),
        nameof(ProfileUpdatePerMinute),
        nameof(ReactionPerMinute),
        nameof(TestLoginPerMinute),
    ];
}

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>Policy names for <c>RequireRateLimiting</c>. Each name keeps its own counters.</summary>
public static class ChatRateLimitPolicies
{
    public const string UserSearch = "users-search";
    public const string RoomDiscover = "rooms-discover";
    public const string RoomMembership = "rooms-membership";
    public const string DirectConversationCreate = "direct-conversations-create";
    public const string RoomCreate = "rooms-create";
    public const string ProfileUpdate = "profile-update";

    /// <summary>The test sign-in, counted per client address: it has no session to count by.</summary>
    public const string TestLogin = "auth-test-login";
}

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>Policy names for <c>RequireRateLimiting</c>. Each name keeps its own counters per user.</summary>
public static class ChatRateLimitPolicies
{
    public const string UserSearch = "users-search";
    public const string RoomDiscover = "rooms-discover";
    public const string RoomMembership = "rooms-membership";
    public const string DirectConversationCreate = "direct-conversations-create";
    public const string RoomCreate = "rooms-create";
}

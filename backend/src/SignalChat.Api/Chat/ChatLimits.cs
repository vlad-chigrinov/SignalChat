namespace SignalChat.Api.Chat;

/// <summary>Page sizes of the list endpoints (message history uses ChatPaging in Infrastructure).</summary>
public static class ChatLimits
{
    public const int DiscoverDefaultLimit = 20;
    public const int DiscoverMaxLimit = 50;
    public const int UserSearchDefaultLimit = 10;
    public const int UserSearchMaxLimit = 20;
}

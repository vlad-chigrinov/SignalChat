namespace SignalChat.Infrastructure.Chat;

/// <summary>Message history page sizes. The hub and the REST API share them.</summary>
public static class ChatPaging
{
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;
}

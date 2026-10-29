namespace SignalChat.Api.Hubs;

/// <summary>
/// Named limits of the chat hub. The message text length limit is <c>Message.MaxTextLength</c>, not repeated here.
/// </summary>
public static class ChatHubLimits
{
    /// <summary>Messages a user may send inside one sliding window.</summary>
    public const int SendRateLimitPermits = 20;

    public static readonly TimeSpan SendRateLimitWindow = TimeSpan.FromSeconds(10);

    /// <summary>Conversations one connection may be joined to at the same time.</summary>
    public const int MaxJoinedConversationsPerConnection = 100;

    /// <summary>Minimum gap between two accepted typing signals of one user in one conversation.</summary>
    public static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(2);

    public const int MaxReceiveMessageSizeBytes = 32 * 1024;
}

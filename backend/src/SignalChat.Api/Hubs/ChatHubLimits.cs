namespace SignalChat.Api.Hubs;

/// <summary>
/// Named limits of the chat hub. The message text length limit is <c>Message.MaxTextLength</c>, not repeated here.
/// </summary>
public static class ChatHubLimits
{
    /// <summary>Messages a user may send inside one sliding window.</summary>
    public const int SendRateLimitPermits = 20;

    public static readonly TimeSpan SendRateLimitWindow = TimeSpan.FromSeconds(10);

    /// <summary>JoinRoom and LeaveRoom calls one user may make inside one window, across all of their connections.</summary>
    public const int MembershipChangePermits = 30;

    public static readonly TimeSpan MembershipChangeWindow = TimeSpan.FromSeconds(10);

    /// <summary>Open hub connections one user may hold at the same time (tabs and devices). Further ones are refused.</summary>
    public const int MaxConnectionsPerUser = 5;

    /// <summary>Conversations one connection may be joined to at the same time.</summary>
    public const int MaxJoinedConversationsPerConnection = 100;

    /// <summary>Minimum gap between two accepted typing signals of one user in one conversation.</summary>
    public static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(2);

    /// <summary>How long a user's last closed connection keeps them online before "offline" is announced.</summary>
    public static readonly TimeSpan PresenceOfflineGrace = TimeSpan.FromSeconds(5);

    public const int MaxReceiveMessageSizeBytes = 32 * 1024;
}

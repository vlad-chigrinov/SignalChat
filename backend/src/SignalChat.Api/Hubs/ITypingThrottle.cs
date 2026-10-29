namespace SignalChat.Api.Hubs;

/// <summary>Per user and conversation: lets a typing signal through at most once per <see cref="ChatHubLimits.TypingThrottle"/>.</summary>
public interface ITypingThrottle
{
    /// <summary>True when the signal may be broadcast; false when it must be dropped silently.</summary>
    bool TryPass(Guid userId, Guid conversationId);
}

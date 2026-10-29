namespace SignalChat.Api.Hubs;

/// <summary>Sliding-window limit on messages per user, with the limits of <see cref="ChatHubLimits"/> (singleton).</summary>
public sealed class SlidingWindowMessageRateLimiter(TimeProvider timeProvider) : IMessageRateLimiter
{
    private readonly SlidingWindowRateLimiter _window = new(
        timeProvider, ChatHubLimits.SendRateLimitPermits, ChatHubLimits.SendRateLimitWindow);

    public bool TryAcquire(Guid userId) => _window.TryAcquire(userId);
}

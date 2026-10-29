namespace SignalChat.Api.Hubs;

/// <summary>Join and leave limit per user, with the limits of <see cref="ChatHubLimits"/> (singleton).</summary>
public sealed class MembershipChangeRateLimiter(TimeProvider timeProvider) : IMembershipChangeRateLimiter
{
    private readonly SlidingWindowRateLimiter _window = new(
        timeProvider, ChatHubLimits.MembershipChangePermits, ChatHubLimits.MembershipChangeWindow);

    public bool TryAcquire(Guid userId) => _window.TryAcquire(userId);
}

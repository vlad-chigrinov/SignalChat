using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Unit tests of the in-memory hub pieces, with the clock moved by hand.</summary>
public sealed class HubLimitersAndTrackerTests
{
    [Fact]
    public void RateLimiter_AllowsThePermits_ThenRefuses_UntilTheWindowPasses()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new SlidingWindowMessageRateLimiter(clock);
        var user = Guid.NewGuid();

        var accepted = Enumerable.Range(0, ChatHubLimits.SendRateLimitPermits).Count(_ => limiter.TryAcquire(user));
        Assert.Equal(ChatHubLimits.SendRateLimitPermits, accepted);
        Assert.False(limiter.TryAcquire(user));

        clock.Advance(ChatHubLimits.SendRateLimitWindow);

        Assert.True(limiter.TryAcquire(user));
    }

    [Fact]
    public void RateLimiter_CountsUsersSeparately()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new SlidingWindowMessageRateLimiter(clock);
        for (var sent = 0; sent < ChatHubLimits.SendRateLimitPermits; sent++)
        {
            limiter.TryAcquire(Guid.NewGuid());
        }
        var other = Guid.NewGuid();

        Assert.True(limiter.TryAcquire(other));
    }

    [Fact]
    public void TypingThrottle_LetsOneThroughPerInterval_PerUserAndConversation()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new InMemoryTypingThrottle(clock);
        var user = Guid.NewGuid();
        var room = Guid.NewGuid();

        Assert.True(throttle.TryPass(user, room));
        Assert.False(throttle.TryPass(user, room));
        Assert.True(throttle.TryPass(user, Guid.NewGuid()));
        clock.Advance(ChatHubLimits.TypingThrottle);

        Assert.True(throttle.TryPass(user, room));
    }

    [Fact]
    public void PresenceTracker_ReportsFirstAndLastConnectionOnly()
    {
        var tracker = new InMemoryPresenceTracker();
        var user = Guid.NewGuid();

        Assert.True(tracker.ConnectionOpened(user));
        Assert.False(tracker.ConnectionOpened(user));
        Assert.True(tracker.IsOnline(user));

        Assert.False(tracker.ConnectionClosed(user));
        Assert.True(tracker.IsOnline(user));
        Assert.True(tracker.ConnectionClosed(user));
        Assert.False(tracker.IsOnline(user));
    }

    [Fact]
    public void PresenceTracker_ClosingAnUnknownConnection_IsNotAnOfflineTransition()
    {
        var tracker = new InMemoryPresenceTracker();

        Assert.False(tracker.ConnectionClosed(Guid.NewGuid()));
    }
}

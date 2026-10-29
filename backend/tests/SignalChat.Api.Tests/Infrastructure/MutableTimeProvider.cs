namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>A clock tests move forward by hand, to exercise lifetimes without waiting.</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

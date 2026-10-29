namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>A one-shot timer that fires when its <see cref="MutableTimeProvider"/> moves past its due time.</summary>
public sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt, Action<ManualTimer> onDispose)
    : ITimer
{
    public TimerCallback Callback { get; } = callback;

    public object? State { get; } = state;

    public DateTimeOffset DueAt { get; } = dueAt;

    public bool Change(TimeSpan dueTime, TimeSpan period) =>
        throw new NotSupportedException("Test timers are created once and never rescheduled.");

    public void Dispose() => onDispose(this);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

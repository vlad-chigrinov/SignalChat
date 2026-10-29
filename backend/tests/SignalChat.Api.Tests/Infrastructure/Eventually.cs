namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>Waits for a condition that the server reaches on its own schedule (a disconnect being processed, say).</summary>
public static class Eventually
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    public static async Task UntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {because}.");
            }
            await Task.Delay(PollInterval);
        }
    }
}

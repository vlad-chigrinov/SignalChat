using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Turns connection counts into presence events. "Online" is announced with the first connection. "Offline" is
/// announced only after the last connection has been gone for <see cref="ChatHubLimits.PresenceOfflineGrace"/>,
/// so a quick reconnect (a page reload, a network blip) stays silent. Singleton. One gate serializes every change,
/// so connects and disconnects of all users cannot interleave their counts and announcements.
/// </summary>
public sealed class PresenceAnnouncer(
    IPresenceTracker tracker, PresenceNotifier notifier, TimeProvider clock, ILogger<PresenceAnnouncer> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ITimer> _pendingOffline = new();

    /// <summary>
    /// Counts a new connection. Call it only after the connection's presence mark is set, so the disconnect always
    /// finds that mark. The wait is not cancellable: a cancelled wait would leave the count out of step with the mark.
    /// </summary>
    public async Task ConnectedAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            var firstConnection = tracker.ConnectionOpened(userId);
            var returnedWithinGrace = CancelPendingOffline(userId);
            if (firstConnection && !returnedWithinGrace)
            {
                await notifier.NotifyAsync(userId, isOnline: true, CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Counts a closed connection. The last one starts the offline grace period instead of announcing at once.</summary>
    public async Task DisconnectedAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!tracker.ConnectionClosed(userId))
            {
                return;
            }

            var timer = clock.CreateTimer(
                OfflineGraceElapsed, userId, ChatHubLimits.PresenceOfflineGrace, Timeout.InfiniteTimeSpan);
            _pendingOffline[userId] = timer;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Online for a snapshot while the user's last connection is still inside its grace period.</summary>
    public bool IsOnline(Guid userId) => tracker.IsOnline(userId) || _pendingOffline.ContainsKey(userId);

    public bool HasPendingOffline(Guid userId) => _pendingOffline.ContainsKey(userId);

    private void OfflineGraceElapsed(object? state) => _ = AnnounceOfflineIfStillAwayAsync((Guid)state!);

    private async Task AnnounceOfflineIfStillAwayAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!CancelPendingOffline(userId))
            {
                return;
            }

            await notifier.NotifyAsync(userId, isOnline: false, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not announce that a user went offline.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Drops a pending offline announcement. True when there was one, so the caller knows the user came back.</summary>
    private bool CancelPendingOffline(Guid userId)
    {
        if (!_pendingOffline.TryRemove(userId, out var timer))
        {
            return false;
        }

        timer.Dispose();
        return true;
    }
}

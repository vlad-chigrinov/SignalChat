using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Collects one server event on a client, so a test can wait for it or prove that none arrives.</summary>
internal sealed class HubEventInbox<T>
{
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SilenceWindow = TimeSpan.FromMilliseconds(300);

    private readonly Channel<T> _events = Channel.CreateUnbounded<T>();

    public HubEventInbox(HubConnection connection, string eventName) =>
        connection.On<T>(eventName, value => _events.Writer.TryWrite(value));

    public async Task<T> NextAsync()
    {
        using var timeout = new CancellationTokenSource(EventTimeout);
        return await _events.Reader.ReadAsync(timeout.Token);
    }

    /// <summary>Passes when no event arrives within the silence window; fails with the event otherwise.</summary>
    public async Task AssertSilenceAsync()
    {
        using var window = new CancellationTokenSource(SilenceWindow);
        try
        {
            var unexpected = await _events.Reader.ReadAsync(window.Token);
            Assert.Fail($"Unexpected event: {unexpected}");
        }
        catch (OperationCanceledException)
        {
            // Expected: nothing arrived inside the window.
        }
    }
}

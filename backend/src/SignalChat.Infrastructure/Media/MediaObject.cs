namespace SignalChat.Infrastructure.Media;

/// <summary>An open object of the store: its bytes as a stream and their count. Disposing it releases the connection to the store.</summary>
public sealed class MediaObject(Stream content, long length, IDisposable? owner = null) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public long Length { get; } = length;

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        owner?.Dispose();
    }
}

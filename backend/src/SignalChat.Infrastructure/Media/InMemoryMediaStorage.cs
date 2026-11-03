using System.Collections.Concurrent;

namespace SignalChat.Infrastructure.Media;

/// <summary>
/// Keeps the objects in memory. For tests and for local development without a RustFS container; production refuses to
/// start with it (see <see cref="MediaStorageRegistration"/>), because the objects would vanish with the process.
/// </summary>
public sealed class InMemoryMediaStorage : IMediaStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToList();

    public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        _objects[key] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<MediaObject?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        var found = _objects.TryGetValue(key, out var bytes);
        return Task.FromResult(found ? new MediaObject(new MemoryStream(bytes!, writable: false), bytes!.Length) : null);
    }

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            _objects.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}

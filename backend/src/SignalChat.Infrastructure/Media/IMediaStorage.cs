namespace SignalChat.Infrastructure.Media;

/// <summary>
/// The object store behind chat photos: a flat set of objects under keys. Production uses S3-compatible RustFS
/// (<see cref="S3MediaStorage"/>); development and tests use <see cref="InMemoryMediaStorage"/>.
/// Every failure of the store itself is a <see cref="MediaStorageException"/>.
/// </summary>
public interface IMediaStorage
{
    /// <summary>Writes the object, replacing one with the same key.</summary>
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens the object for reading, or returns null when there is none. The caller disposes the result.</summary>
    Task<MediaObject?> OpenAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the objects; a key that does not exist is not an error.</summary>
    Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);
}

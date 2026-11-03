namespace SignalChat.Infrastructure.Media;

/// <summary>The object store did not do what it was asked: it is down, refused the request, or answered with an error.</summary>
public sealed class MediaStorageException(string message, Exception? inner = null) : Exception(message, inner);

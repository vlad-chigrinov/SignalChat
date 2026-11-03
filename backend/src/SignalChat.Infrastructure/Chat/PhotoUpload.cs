namespace SignalChat.Infrastructure.Chat;

/// <summary>One file of a photo message as it arrived: the uploaded name (not trusted) and the bytes (not trusted either).</summary>
public sealed record PhotoUpload(string? FileName, ReadOnlyMemory<byte> Content);

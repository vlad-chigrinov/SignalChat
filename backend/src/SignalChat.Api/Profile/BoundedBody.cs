namespace SignalChat.Api.Profile;

/// <summary>Reads a request body without ever holding more than a limit, whatever the client claims about its length.</summary>
internal static class BoundedBody
{
    /// <summary>
    /// The first bytes of the body, at most <paramref name="limit"/> plus one: one byte past the limit is enough to tell
    /// the caller the body is too large, and the rest is never read.
    /// </summary>
    public static async Task<byte[]> ReadAsync(Stream body, int limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[limit + 1];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                break;
            }
            filled += read;
        }
        return filled == buffer.Length ? buffer : buffer[..filled];
    }
}

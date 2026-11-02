using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace SignalChat.Api.Configuration;

/// <summary>
/// Where ASP.NET Data Protection keeps its keys. The session cookie and the Google sign-in cookies are encrypted with
/// them. Read from <c>DATA_PROTECTION_KEYS_PATH</c>, a directory on a volume that survives redeploys. In Production the
/// path is required: with keys only in memory, every restart signs every user out, so the host refuses to start.
/// </summary>
public sealed class DataProtectionSettings
{
    public const string KeysPathKey = "DATA_PROTECTION_KEYS_PATH";

    public const string ApplicationName = "SignalChat";

    private DataProtectionSettings(string? keysPath)
    {
        KeysPath = keysPath;
    }

    /// <summary>Directory for the key ring, or null when the keys stay in memory (only outside Production).</summary>
    public string? KeysPath { get; }

    public static DataProtectionSettings FromConfiguration(IConfiguration configuration, bool isProduction)
    {
        var keysPath = configuration[KeysPathKey];
        var configured = !string.IsNullOrWhiteSpace(keysPath);
        if (!configured && isProduction)
        {
            throw new InvalidOperationException(
                $"{KeysPathKey} is required in Production: without a directory on a persistent volume the session keys are "
                + "lost on every restart. Mount a volume and point the variable at it.");
        }

        return new DataProtectionSettings(configured ? keysPath : null);
    }

    public void ApplyTo(IDataProtectionBuilder builder)
    {
        builder.SetApplicationName(ApplicationName);
        if (KeysPath is not null)
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(KeysPath));
        }
    }
}

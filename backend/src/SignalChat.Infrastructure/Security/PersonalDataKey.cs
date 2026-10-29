using Microsoft.Extensions.Configuration;

namespace SignalChat.Infrastructure.Security;

/// <summary>Reads the base64 AES-256 key for personal data from <c>PII_ENCRYPTION_KEY</c>.</summary>
public static class PersonalDataKey
{
    public const string ConfigurationKey = "PII_ENCRYPTION_KEY";

    public static byte[] FromConfiguration(IConfiguration configuration)
    {
        var encoded = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is missing. Run scripts/dev.* once, or set a base64 32-byte key in .env.");
        }

        try
        {
            var key = Convert.FromBase64String(encoded.Trim());
            if (key.Length == AesGcmPersonalDataCipher.KeySizeBytes)
            {
                return key;
            }
        }
        catch (FormatException)
        {
            // Reported below with the same message as a wrong length.
        }

        throw new InvalidOperationException(
            $"{ConfigurationKey} must be base64 of exactly {AesGcmPersonalDataCipher.KeySizeBytes} random bytes.");
    }
}

using System.Security.Cryptography;
using System.Text;

namespace SignalChat.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a random nonce per value. Stored format: <c>v1:</c> + base64(nonce | ciphertext | tag).
/// The version prefix leaves room for key rotation.
/// </summary>
public sealed class AesGcmPersonalDataCipher : IPersonalDataCipher
{
    public const int KeySizeBytes = 32;
    private const string FormatPrefix = "v1:";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmPersonalDataCipher(byte[] key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"Personal data key must be {KeySizeBytes} bytes.", nameof(key));
        }
        _key = key.ToArray();
    }

    public string Encrypt(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSizeBytes + plainBytes.Length + TagSizeBytes];
        var nonce = payload.AsSpan(0, NonceSizeBytes);
        var cipherBytes = payload.AsSpan(NonceSizeBytes, plainBytes.Length);
        var tag = payload.AsSpan(NonceSizeBytes + plainBytes.Length, TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return FormatPrefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        if (!ciphertext.StartsWith(FormatPrefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown personal data format.");
        }

        var payload = Convert.FromBase64String(ciphertext[FormatPrefix.Length..]);
        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Personal data payload is truncated.");
        }

        var cipherLength = payload.Length - NonceSizeBytes - TagSizeBytes;
        var plainBytes = new byte[cipherLength];
        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(
            payload.AsSpan(0, NonceSizeBytes),
            payload.AsSpan(NonceSizeBytes, cipherLength),
            payload.AsSpan(NonceSizeBytes + cipherLength, TagSizeBytes),
            plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}

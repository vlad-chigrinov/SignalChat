namespace SignalChat.Infrastructure.Security;

/// <summary>Encrypts personal data before it is written to the database.</summary>
public interface IPersonalDataCipher
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SignalChat.Infrastructure.Security;

namespace SignalChat.Infrastructure.Persistence;

/// <summary>Stores a string column encrypted; the domain only ever sees plaintext.</summary>
public sealed class EncryptedStringConverter(IPersonalDataCipher cipher)
    : ValueConverter<string, string>(plain => cipher.Encrypt(plain), stored => cipher.Decrypt(stored));

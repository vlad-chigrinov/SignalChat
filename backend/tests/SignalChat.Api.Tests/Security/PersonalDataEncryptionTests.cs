using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Security;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Security;

public sealed class PersonalDataEncryptionTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    private const string Email = "secret.person@example.com";

    [Fact]
    public async Task Email_IsStoredEncrypted_AndReadBackInPlaintext()
    {
        var (user, _) = await factory.CreateSessionAsync(new GoogleProfile("pii-sub", Email, "Pii", null));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        var stored = await db.Database
            .SqlQuery<string>($"SELECT Email AS Value FROM users WHERE Id = {user.Id}")
            .SingleAsync();

        Assert.DoesNotContain("secret.person", stored);
        Assert.StartsWith("v1:", stored);
        Assert.Equal(Email, (await db.Users.SingleAsync(existing => existing.Id == user.Id)).Email);
    }

    [Fact]
    public void Cipher_UsesAFreshNonce_SoEqualEmailsDoNotLookEqual()
    {
        var cipher = NewCipher();

        Assert.NotEqual(cipher.Encrypt(Email), cipher.Encrypt(Email));
    }

    [Fact]
    public void Cipher_RejectsTamperedCiphertext()
    {
        var cipher = NewCipher();
        var stored = cipher.Encrypt(Email).ToCharArray();
        stored[^5] = stored[^5] == 'A' ? 'B' : 'A';

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(new string(stored)));
    }

    [Fact]
    public void Cipher_WithAnotherKey_CannotRead()
    {
        var stored = NewCipher().Encrypt(Email);

        Assert.ThrowsAny<CryptographicException>(() => NewCipher().Decrypt(stored));
    }

    private static AesGcmPersonalDataCipher NewCipher() =>
        new(RandomNumberGenerator.GetBytes(AesGcmPersonalDataCipher.KeySizeBytes));
}

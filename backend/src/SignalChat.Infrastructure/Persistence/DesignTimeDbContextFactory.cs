using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SignalChat.Infrastructure.Security;

namespace SignalChat.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef migrations add</c> only: building the model needs no database and no secrets,
/// so a placeholder connection string and a throwaway key are enough.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SignalChatDbContext>
{
    private const string PlaceholderConnectionString = "Host=localhost;Database=design_time";

    public SignalChatDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SignalChatDbContext>()
            .UseNpgsql(PlaceholderConnectionString)
            .Options;
        var throwawayKey = RandomNumberGenerator.GetBytes(AesGcmPersonalDataCipher.KeySizeBytes);
        return new SignalChatDbContext(options, new AesGcmPersonalDataCipher(throwawayKey));
    }
}

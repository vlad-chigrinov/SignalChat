using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Persistence.Configurations;
using SignalChat.Infrastructure.Security;

namespace SignalChat.Infrastructure.Persistence;

/// <summary>EF Core unit of work for SignalChat.</summary>
public sealed class SignalChatDbContext(
    DbContextOptions<SignalChatDbContext> options,
    IPersonalDataCipher personalDataCipher) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration(personalDataCipher));
    }
}

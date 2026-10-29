using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
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

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    public DbSet<DirectConversation> DirectConversations => Set<DirectConversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration(personalDataCipher));
        modelBuilder.ApplyConfiguration(new RoomConfiguration());
        modelBuilder.ApplyConfiguration(new RoomMemberConfiguration());
        modelBuilder.ApplyConfiguration(new DirectConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
    }
}

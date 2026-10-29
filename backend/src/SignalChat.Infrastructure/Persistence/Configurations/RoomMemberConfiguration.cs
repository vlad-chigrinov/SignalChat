using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class RoomMemberConfiguration : IEntityTypeConfiguration<RoomMember>
{
    public void Configure(EntityTypeBuilder<RoomMember> builder)
    {
        builder.ToTable("room_members");
        builder.HasKey(member => new { member.RoomId, member.UserId });

        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(member => member.JoinedAt).IsRequired();

        // "My rooms" lookups go by user.
        builder.HasIndex(member => member.UserId);
        builder.HasOne<Room>().WithMany().HasForeignKey(member => member.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

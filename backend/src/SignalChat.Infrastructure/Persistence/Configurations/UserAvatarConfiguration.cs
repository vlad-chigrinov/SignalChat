using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Users;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class UserAvatarConfiguration : IEntityTypeConfiguration<UserAvatar>
{
    public void Configure(EntityTypeBuilder<UserAvatar> builder)
    {
        builder.ToTable("user_avatars");
        builder.HasKey(avatar => avatar.UserId);
        builder.Property(avatar => avatar.UserId).ValueGeneratedNever();

        builder.Property(avatar => avatar.Image).IsRequired();
        builder.Property(avatar => avatar.UpdatedAt).IsRequired();

        builder.HasOne<User>().WithOne().HasForeignKey<UserAvatar>(avatar => avatar.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

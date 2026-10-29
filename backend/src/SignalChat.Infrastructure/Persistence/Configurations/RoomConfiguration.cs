using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Id).ValueGeneratedNever();

        builder.Property(room => room.Name).HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.Property(room => room.NormalizedName).HasMaxLength(Room.NameMaxLength).IsRequired();
        // Unique regardless of letter case: the key is the lower-cased name.
        builder.HasIndex(room => room.NormalizedName).IsUnique();

        builder.Property(room => room.Description).HasMaxLength(Room.DescriptionMaxLength).IsRequired();
        builder.Property(room => room.CreatedAt).IsRequired();
        builder.Property(room => room.CreatedByUserId).IsRequired();
    }
}

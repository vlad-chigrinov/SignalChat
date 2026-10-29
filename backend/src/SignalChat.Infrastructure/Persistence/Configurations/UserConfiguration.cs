using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Security;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration(IPersonalDataCipher cipher) : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.GoogleSubject).HasMaxLength(User.GoogleSubjectMaxLength).IsRequired();
        builder.HasIndex(user => user.GoogleSubject).IsUnique();

        // Ciphertext: never queried, never indexed.
        builder.Property(user => user.Email).HasConversion(new EncryptedStringConverter(cipher)).IsRequired();

        builder.Property(user => user.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.AvatarUrl).HasMaxLength(User.AvatarUrlMaxLength);
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.LastLoginAt).IsRequired();
        // Existing rows get the initial version when the column is added.
        builder.Property(user => user.SessionVersion).IsRequired().HasDefaultValue(User.InitialSessionVersion);
    }
}

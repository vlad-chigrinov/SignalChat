using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    private const int MaxKeyLength = 200;
    private const int MaxContentTypeLength = 100;
    private const int MaxFileNameLength = 120;

    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("message_attachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).ValueGeneratedNever();

        builder.Property(attachment => attachment.MessageId).IsRequired();
        builder.Property(attachment => attachment.Position).IsRequired();
        builder.Property(attachment => attachment.OriginalKey).HasMaxLength(MaxKeyLength).IsRequired();
        builder.Property(attachment => attachment.ThumbnailKey).HasMaxLength(MaxKeyLength).IsRequired();
        builder.Property(attachment => attachment.Width).IsRequired();
        builder.Property(attachment => attachment.Height).IsRequired();
        builder.Property(attachment => attachment.ThumbnailWidth).IsRequired();
        builder.Property(attachment => attachment.ThumbnailHeight).IsRequired();
        builder.Property(attachment => attachment.SizeBytes).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(MaxContentTypeLength).IsRequired();
        builder.Property(attachment => attachment.FileName).HasMaxLength(MaxFileNameLength).IsRequired();

        // A message's photos in display order, and one position per message.
        builder.HasIndex(attachment => new { attachment.MessageId, attachment.Position }).IsUnique();
    }
}

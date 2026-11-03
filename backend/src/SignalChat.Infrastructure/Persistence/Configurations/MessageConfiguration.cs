using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    private const string ExactlyOneConversationCheck =
        "(\"RoomId\" IS NULL) <> (\"DirectConversationId\" IS NULL)";

    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", table => table.HasCheckConstraint(
            "CK_messages_exactly_one_conversation", ExactlyOneConversationCheck));
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Ignore(message => message.ConversationId);

        builder.Property(message => message.Text).HasMaxLength(Message.MaxTextLength).IsRequired();
        builder.Property(message => message.AuthorId).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();

        builder.HasMany(message => message.Attachments).WithOne().HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(message => message.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Room>().WithMany().HasForeignKey(message => message.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DirectConversation>().WithMany()
            .HasForeignKey(message => message.DirectConversationId).OnDelete(DeleteBehavior.Cascade);

        // History pages: newest first by (CreatedAt, Id), one index per conversation kind.
        builder.HasIndex(message => new { message.RoomId, message.CreatedAt, message.Id })
            .IsDescending(false, true, true);
        builder.HasIndex(message => new { message.DirectConversationId, message.CreatedAt, message.Id })
            .IsDescending(false, true, true);
    }
}

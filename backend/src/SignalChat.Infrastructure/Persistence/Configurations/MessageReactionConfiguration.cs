using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("message_reactions");

        // The key is the rule "one reaction per user and message": the database refuses a second row for the pair.
        // Its first column also serves the history query, which reads the reactions of a page of messages.
        builder.HasKey(reaction => new { reaction.MessageId, reaction.UserId });

        builder.Property(reaction => reaction.Emoji).HasMaxLength(ReactionCatalog.MaxEmojiLength).IsRequired();
        builder.Property(reaction => reaction.CreatedAt).IsRequired();

        builder.HasOne<Message>().WithMany().HasForeignKey(reaction => reaction.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Persistence.Configurations;

internal sealed class DirectConversationConfiguration : IEntityTypeConfiguration<DirectConversation>
{
    public void Configure(EntityTypeBuilder<DirectConversation> builder)
    {
        builder.ToTable("direct_conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();

        builder.Property(conversation => conversation.UserLowId).IsRequired();
        builder.Property(conversation => conversation.UserHighId).IsRequired();
        builder.Property(conversation => conversation.InitiatorId).IsRequired();
        builder.Property(conversation => conversation.Status).IsRequired().HasConversion<string>();
        builder.Property(conversation => conversation.RespondedAt);
        builder.Property(conversation => conversation.CreatedAt).IsRequired();

        // One conversation per pair of users, whichever side starts it.
        builder.HasIndex(conversation => new { conversation.UserLowId, conversation.UserHighId }).IsUnique();
        builder.HasIndex(conversation => conversation.UserHighId);
        // Counts a user's outgoing requests (the per-user limit) without scanning their conversations.
        builder.HasIndex(conversation => new { conversation.InitiatorId, conversation.Status });
    }
}

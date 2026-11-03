using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class ReactionStore(
    SignalChatDbContext dbContext,
    IConversationAccess access,
    TimeProvider timeProvider) : IReactionStore
{
    /// <summary>A second attempt covers two requests of the same user racing to insert the same pair.</summary>
    private const int MaxWriteAttempts = 2;

    public async Task<ReactionChange> ChooseAsync(
        Guid userId, Guid conversationId, Guid messageId, string? emoji, CancellationToken cancellationToken)
    {
        var conversation = await access.FindActiveAsync(userId, conversationId, cancellationToken)
            ?? throw await NotActiveAsync(userId, conversationId, cancellationToken);
        if (!await MessageIsVisibleInAsync(conversation, messageId, cancellationToken))
        {
            throw new ChatRuleException("not_found", "Message not found.");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ApplyChoiceAsync(userId, messageId, emoji, cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt < MaxWriteAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        var aggregates = await ReactionAggregates.LoadAsync(dbContext, [messageId], userId, cancellationToken);
        return new ReactionChange(conversation, messageId, aggregates.GetValueOrDefault(messageId) ?? []);
    }

    private async Task ApplyChoiceAsync(Guid userId, Guid messageId, string? emoji, CancellationToken cancellationToken)
    {
        var current = await dbContext.MessageReactions.SingleOrDefaultAsync(
            reaction => reaction.MessageId == messageId && reaction.UserId == userId, cancellationToken);

        if (current is null)
        {
            dbContext.MessageReactions.Add(MessageReaction.Create(messageId, userId, emoji, timeProvider.GetUtcNow()));
        }
        else if (string.Equals(current.Emoji, emoji, StringComparison.Ordinal))
        {
            dbContext.MessageReactions.Remove(current);
        }
        else
        {
            current.ChangeTo(emoji);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> MessageIsVisibleInAsync(ConversationRef conversation, Guid messageId, CancellationToken cancellationToken)
    {
        var query = dbContext.Messages.AsNoTracking().Where(message => message.Id == messageId && message.DeletedAt == null);
        query = conversation.Kind == ConversationKind.Room
            ? query.Where(message => message.RoomId == conversation.Id)
            : query.Where(message => message.DirectConversationId == conversation.Id);
        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>The initiator of a pending request sees the chat but cannot react in it; everyone else gets "not_found".</summary>
    private async Task<ChatRuleException> NotActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await access.FindForMemberAsync(userId, conversationId, cancellationToken) is not null
            ? new ChatRuleException(DirectRequestErrorCodes.NotAccepted, "The request has not been accepted yet.")
            : new ChatRuleException("not_found", "Conversation not found.");
}

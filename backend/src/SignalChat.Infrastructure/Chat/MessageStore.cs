using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class MessageStore(
    SignalChatDbContext dbContext,
    IConversationAccess access,
    TimeProvider timeProvider) : IMessageStore
{
    public async Task<MessageView> AppendAsync(Guid userId, Guid conversationId, string? text, CancellationToken cancellationToken)
    {
        // Writing needs an active conversation: a pending request accepts no messages from its initiator.
        var conversation = await access.FindActiveAsync(userId, conversationId, cancellationToken)
            ?? throw new ChatRuleException("not_found", "Conversation not found.");

        var now = timeProvider.GetUtcNow();
        var message = conversation.Kind == ConversationKind.Room
            ? Message.InRoom(conversationId, userId, text, now)
            : Message.InDirectConversation(conversationId, userId, text, now);

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var author = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .SingleAsync(cancellationToken);
        return new MessageView(message.Id, conversationId, author, message.Text, message.CreatedAt);
    }

    public async Task<MessagePage> GetPageAsync(
        Guid viewerId, ConversationRef conversation, MessageCursor? before, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.Messages.AsNoTracking().Where(message => message.DeletedAt == null);
        query = conversation.Kind == ConversationKind.Room
            ? query.Where(message => message.RoomId == conversation.Id)
            : query.Where(message => message.DirectConversationId == conversation.Id);

        if (before is { } cursor)
        {
            var cursorSentAt = cursor.SentAt;
            var cursorId = cursor.Id;
            query = query.Where(message =>
                message.CreatedAt < cursorSentAt
                || (message.CreatedAt == cursorSentAt && message.Id < cursorId));
        }

        // limit + 1 rows tell whether an older page exists without a second query.
        var rows = await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasOlder = rows.Count > limit;
        var pageRows = rows.Take(limit).Reverse().ToList();
        var authors = await LoadAuthorsAsync(pageRows.Select(row => row.AuthorId).Distinct().ToList(), cancellationToken);

        var reactions = await ReactionAggregates.LoadAsync(
            dbContext, pageRows.Select(row => row.Id).ToList(), viewerId, cancellationToken);

        var items = pageRows
            .Select(row => new MessageView(row.Id, conversation.Id, authors[row.AuthorId], row.Text, row.CreatedAt)
            {
                Reactions = reactions.GetValueOrDefault(row.Id) ?? [],
            })
            .ToList();
        var nextCursor = hasOlder && pageRows.Count > 0
            ? new MessageCursor(pageRows[0].CreatedAt, pageRows[0].Id).Encode()
            : null;
        return new MessagePage(items, nextCursor);
    }

    private async Task<Dictionary<Guid, ChatUser>> LoadAuthorsAsync(IReadOnlyCollection<Guid> authorIds, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => authorIds.Contains(user.Id))
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Id);
    }
}

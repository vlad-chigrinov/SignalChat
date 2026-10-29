using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class DirectConversationService(SignalChatDbContext dbContext, TimeProvider timeProvider) : IDirectConversationService
{
    public async Task<IReadOnlyList<DirectConversationSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await RowsFor(userId).ToListAsync(cancellationToken);
        var peers = await LoadPeersAsync(rows.Select(row => row.PeerId).Distinct().ToList(), cancellationToken);
        var lastMessages = await LastMessageLoader.LoadAsync(
            dbContext, rows.Select(row => row.LastMessageId).OfType<Guid>().ToList(), cancellationToken);

        return rows
            .Select(row => (Row: row, Summary: new DirectConversationSummary(
                row.Id, peers[row.PeerId], LastMessageLoader.Find(lastMessages, row.LastMessageId))))
            .OrderByDescending(item => item.Summary.LastMessage?.SentAt ?? item.Row.CreatedAt)
            .Select(item => item.Summary)
            .ToList();
    }

    public async Task<DirectConversationSummary> GetOrCreateAsync(Guid userId, Guid peerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidate = DirectConversation.Between(userId, peerId, now);

        if (!await dbContext.Users.AnyAsync(user => user.Id == peerId, cancellationToken))
        {
            throw new ChatRuleException("not_found", "User not found.");
        }

        var conversation = await FindPairAsync(candidate.UserLowId, candidate.UserHighId, cancellationToken);
        if (conversation is null)
        {
            dbContext.DirectConversations.Add(candidate);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                conversation = candidate;
            }
            catch (DbUpdateException)
            {
                // The other side of the pair created it concurrently: return that one.
                var concurrent = await FindPairAsync(candidate.UserLowId, candidate.UserHighId, cancellationToken);
                if (concurrent is null)
                {
                    throw;
                }
                conversation = concurrent;
            }
        }

        var row = await RowsFor(userId, conversation.Id).SingleAsync(cancellationToken);
        var peer = (await LoadPeersAsync([row.PeerId], cancellationToken))[row.PeerId];
        var lastMessages = await LastMessageLoader.LoadAsync(dbContext, CollectIds(row.LastMessageId), cancellationToken);
        return new DirectConversationSummary(row.Id, peer, LastMessageLoader.Find(lastMessages, row.LastMessageId));
    }

    /// <summary>
    /// The caller's conversations (optionally one of them) with the peer id and the id of the newest message.
    /// The id filter sits before the projection: filtering a projection that holds a subquery does not translate.
    /// </summary>
    private IQueryable<DirectRow> RowsFor(Guid userId, Guid? conversationId = null)
    {
        var conversations = dbContext.DirectConversations.AsNoTracking()
            .Where(conversation => conversation.UserLowId == userId || conversation.UserHighId == userId);
        if (conversationId is { } id)
        {
            conversations = conversations.Where(conversation => conversation.Id == id);
        }

        return conversations
            .Select(conversation => new DirectRow(
                conversation.Id,
                conversation.UserLowId == userId ? conversation.UserHighId : conversation.UserLowId,
                conversation.CreatedAt,
                dbContext.Messages
                    .Where(message => message.DirectConversationId == conversation.Id && message.DeletedAt == null)
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => (Guid?)message.Id)
                    .FirstOrDefault()));
    }

    private Task<DirectConversation?> FindPairAsync(Guid lowId, Guid highId, CancellationToken cancellationToken) =>
        dbContext.DirectConversations.AsNoTracking()
            .SingleOrDefaultAsync(conversation => conversation.UserLowId == lowId && conversation.UserHighId == highId, cancellationToken);

    private async Task<Dictionary<Guid, ChatUser>> LoadPeersAsync(IReadOnlyCollection<Guid> peerIds, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => peerIds.Contains(user.Id))
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Id);
    }

    private static IReadOnlyCollection<Guid> CollectIds(Guid? id) => id is { } value ? [value] : [];

    private sealed record DirectRow(Guid Id, Guid PeerId, DateTimeOffset CreatedAt, Guid? LastMessageId);
}

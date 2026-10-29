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

        return rows
            .OrderByDescending(row => row.LastMessage?.SentAt ?? row.CreatedAt)
            .Select(row => new DirectConversationSummary(row.Id, peers[row.PeerId], row.LastMessage))
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

        var row = await RowsFor(userId).SingleAsync(item => item.Id == conversation.Id, cancellationToken);
        var peer = (await LoadPeersAsync([row.PeerId], cancellationToken))[row.PeerId];
        return new DirectConversationSummary(row.Id, peer, row.LastMessage);
    }

    /// <summary>The caller's conversations with the peer id and the newest message, in one statement.</summary>
    private IQueryable<DirectRow> RowsFor(Guid userId) =>
        dbContext.DirectConversations.AsNoTracking()
            .Where(conversation => conversation.UserLowId == userId || conversation.UserHighId == userId)
            .Select(conversation => new DirectRow(
                conversation.Id,
                conversation.UserLowId == userId ? conversation.UserHighId : conversation.UserLowId,
                conversation.CreatedAt,
                dbContext.Messages
                    .Where(message => message.DirectConversationId == conversation.Id && message.DeletedAt == null)
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => new LastMessageView(message.AuthorId, message.Text, message.CreatedAt))
                    .FirstOrDefault()));

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

    private sealed record DirectRow(Guid Id, Guid PeerId, DateTimeOffset CreatedAt, LastMessageView? LastMessage);
}

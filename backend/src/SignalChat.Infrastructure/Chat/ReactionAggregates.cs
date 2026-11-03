using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

/// <summary>Counts the reactions of a set of messages per emoji, with the viewer's own marked. Two small queries, no user ids leave this class.</summary>
internal static class ReactionAggregates
{
    public static async Task<Dictionary<Guid, IReadOnlyList<ReactionView>>> LoadAsync(
        SignalChatDbContext dbContext, IReadOnlyCollection<Guid> messageIds, Guid viewerId, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
        {
            return [];
        }

        var counts = await dbContext.MessageReactions.AsNoTracking()
            .Where(reaction => messageIds.Contains(reaction.MessageId))
            .GroupBy(reaction => new { reaction.MessageId, reaction.Emoji })
            .Select(group => new { group.Key.MessageId, group.Key.Emoji, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var mine = await dbContext.MessageReactions.AsNoTracking()
            .Where(reaction => messageIds.Contains(reaction.MessageId) && reaction.UserId == viewerId)
            .Select(reaction => new { reaction.MessageId, reaction.Emoji })
            .ToListAsync(cancellationToken);
        var ownEmojiByMessage = mine.ToDictionary(row => row.MessageId, row => row.Emoji);

        return counts
            .GroupBy(row => row.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ReactionView>)group
                    .OrderBy(row => ReactionCatalog.OrderOf(row.Emoji))
                    .Select(row => new ReactionView(
                        row.Emoji,
                        row.Count,
                        ownEmojiByMessage.TryGetValue(group.Key, out var own) && own == row.Emoji))
                    .ToList());
    }
}

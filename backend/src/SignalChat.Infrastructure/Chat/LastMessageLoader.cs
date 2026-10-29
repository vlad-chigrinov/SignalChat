using Microsoft.EntityFrameworkCore;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

/// <summary>
/// Loads the newest message of many conversations in one query. The list queries only select the id of
/// that message (a scalar subquery EF translates reliably); this loader turns those ids into views.
/// </summary>
public static class LastMessageLoader
{
    public static async Task<Dictionary<Guid, LastMessageView>> LoadAsync(
        SignalChatDbContext dbContext, IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Messages.AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .Select(message => new { message.Id, message.AuthorId, message.Text, message.CreatedAt })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => new LastMessageView(row.AuthorId, row.Text, row.CreatedAt));
    }

    public static LastMessageView? Find(IReadOnlyDictionary<Guid, LastMessageView> loaded, Guid? messageId) =>
        messageId is { } id && loaded.TryGetValue(id, out var view) ? view : null;
}

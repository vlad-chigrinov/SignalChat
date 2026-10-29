using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class UserDirectory(SignalChatDbContext dbContext) : IUserDirectory
{
    public const int MinQueryLength = 2;

    public async Task<IReadOnlyList<ChatUser>> SearchAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken)
    {
        var term = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length < MinQueryLength)
        {
            throw new ChatRuleException("query_too_short", $"Search needs at least {MinQueryLength} characters.");
        }

        return await dbContext.Users.AsNoTracking()
            .Where(user => user.Id != userId && user.DisplayName.ToLower().Contains(term))
            .OrderBy(user => user.DisplayName)
            .Take(limit)
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class ConversationAccess(SignalChatDbContext dbContext) : IConversationAccess
{
    public async Task<ConversationRef?> FindForMemberAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await FindRoomAsync(userId, conversationId, cancellationToken)
        ?? await FindDirectAsync(userId, conversationId, acceptedOnly: false, cancellationToken);

    public async Task<ConversationRef?> FindActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await FindRoomAsync(userId, conversationId, cancellationToken)
        ?? await FindDirectAsync(userId, conversationId, acceptedOnly: true, cancellationToken);

    private async Task<ConversationRef?> FindRoomAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var isRoomMember = await dbContext.RoomMembers.AnyAsync(
            member => member.RoomId == conversationId && member.UserId == userId, cancellationToken);
        return isRoomMember ? new ConversationRef(ConversationKind.Room, conversationId) : null;
    }

    private async Task<ConversationRef?> FindDirectAsync(
        Guid userId, Guid conversationId, bool acceptedOnly, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.DirectConversations.AsNoTracking()
            .Where(row => row.Id == conversationId && (row.UserLowId == userId || row.UserHighId == userId))
            .Select(row => new { row.Status, row.InitiatorId })
            .SingleOrDefaultAsync(cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var visible = acceptedOnly
            ? conversation.Status == DirectConversationStatus.Accepted
            : conversation.Status == DirectConversationStatus.Accepted || conversation.InitiatorId == userId;
        return visible ? new ConversationRef(ConversationKind.Direct, conversationId) : null;
    }
}

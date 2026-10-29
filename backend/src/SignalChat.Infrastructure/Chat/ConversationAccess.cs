using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class ConversationAccess(SignalChatDbContext dbContext) : IConversationAccess
{
    public async Task<ConversationRef?> FindForMemberAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var isRoomMember = await dbContext.RoomMembers.AnyAsync(
            member => member.RoomId == conversationId && member.UserId == userId, cancellationToken);
        if (isRoomMember)
        {
            return new ConversationRef(ConversationKind.Room, conversationId);
        }

        var isDirectParticipant = await dbContext.DirectConversations.AnyAsync(
            conversation => conversation.Id == conversationId
                && (conversation.UserLowId == userId || conversation.UserHighId == userId),
            cancellationToken);
        return isDirectParticipant ? new ConversationRef(ConversationKind.Direct, conversationId) : null;
    }
}

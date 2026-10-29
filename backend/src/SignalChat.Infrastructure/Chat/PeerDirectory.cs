using Microsoft.EntityFrameworkCore;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class PeerDirectory(SignalChatDbContext dbContext) : IPeerDirectory
{
    public async Task<IReadOnlyList<Guid>> GetPeerIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var myRoomIds = dbContext.RoomMembers
            .Where(member => member.UserId == userId)
            .Select(member => member.RoomId);

        var roomPeers = dbContext.RoomMembers
            .Where(member => member.UserId != userId && myRoomIds.Contains(member.RoomId))
            .Select(member => member.UserId);

        var directPeers = dbContext.DirectConversations
            .Where(conversation => conversation.UserLowId == userId)
            .Select(conversation => conversation.UserHighId)
            .Union(dbContext.DirectConversations
                .Where(conversation => conversation.UserHighId == userId)
                .Select(conversation => conversation.UserLowId));

        return await roomPeers.Union(directPeers).ToListAsync(cancellationToken);
    }
}

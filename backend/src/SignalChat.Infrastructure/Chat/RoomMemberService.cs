using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class RoomMemberService(SignalChatDbContext dbContext, TimeProvider timeProvider) : IRoomMemberService
{
    private const string NotFoundCode = "not_found";

    public async Task<IReadOnlyList<RoomMemberView>> ListAsync(
        Guid userId, Guid roomId, int limit, CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(roomId, userId, cancellationToken))
        {
            throw new ChatRuleException(NotFoundCode, "Room not found.");
        }

        // The profile is read from the user row, so a changed name or avatar shows at once. No email is selected.
        var rows = await dbContext.RoomMembers.AsNoTracking()
            .Where(member => member.RoomId == roomId)
            .Join(
                dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member.Role, user.Id, user.DisplayName, user.AvatarUrl })
            .OrderByDescending(row => row.Role)
            .ThenBy(row => row.DisplayName)
            .ThenBy(row => row.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(row => new RoomMemberView(new ChatUser(row.Id, row.DisplayName, row.AvatarUrl), row.Role)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListMemberIdsAsync(Guid roomId, CancellationToken cancellationToken) =>
        await dbContext.RoomMembers.AsNoTracking()
            .Where(member => member.RoomId == roomId)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);

    public async Task<RoomMemberAdded> InviteAsync(
        Guid inviterId, Guid roomId, Guid inviteeId, CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(roomId, inviterId, cancellationToken)
            || !await HaveAcceptedChatAsync(inviterId, inviteeId, cancellationToken))
        {
            throw new ChatRuleException(NotFoundCode, "Room or user not found.");
        }

        if (await IsMemberAsync(roomId, inviteeId, cancellationToken))
        {
            throw AlreadyMember();
        }

        var invitee = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == inviteeId)
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ChatRuleException(NotFoundCode, "Room or user not found.");

        var now = timeProvider.GetUtcNow();
        dbContext.RoomMembers.Add(RoomMember.Join(roomId, inviteeId, RoomRole.Member, now));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request added the same user between the check and the insert: the answer is the same.
            if (await IsMemberAsync(roomId, inviteeId, cancellationToken))
            {
                throw AlreadyMember();
            }
            throw;
        }

        return new RoomMemberAdded(invitee, now);
    }

    private static ChatRuleException AlreadyMember() =>
        new(RoomMemberErrorCodes.AlreadyMember, "The user is already in this room.");

    /// <summary>Only an accepted conversation counts: a waiting or declined request is not a chat.</summary>
    private Task<bool> HaveAcceptedChatAsync(Guid userId, Guid otherId, CancellationToken cancellationToken) =>
        dbContext.DirectConversations.AnyAsync(
            conversation => conversation.Status == DirectConversationStatus.Accepted
                && ((conversation.UserLowId == userId && conversation.UserHighId == otherId)
                    || (conversation.UserLowId == otherId && conversation.UserHighId == userId)),
            cancellationToken);

    private Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.RoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId == userId, cancellationToken);
}

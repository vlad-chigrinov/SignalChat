using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class RoomService(SignalChatDbContext dbContext, TimeProvider timeProvider) : IRoomService
{
    public async Task<RoomSummary> CreateAsync(Guid userId, string? name, string? description, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var room = Room.Create(name, description, userId, now);

        var ownedRooms = await dbContext.RoomMembers.CountAsync(
            member => member.UserId == userId && member.Role == RoomRole.Owner, cancellationToken);
        if (ownedRooms >= Room.MaxOwnedRoomsPerUser)
        {
            throw new ChatRuleException("room_limit_reached", $"A user can own at most {Room.MaxOwnedRoomsPerUser} rooms.");
        }

        if (await NameExistsAsync(room.NormalizedName, cancellationToken))
        {
            throw new ChatRuleException("room_name_taken", "A room with this name already exists.");
        }

        dbContext.Rooms.Add(room);
        dbContext.RoomMembers.Add(RoomMember.Join(room.Id, userId, RoomRole.Owner, now));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request took the name between the check and the insert (unique index).
            if (await NameExistsAsync(room.NormalizedName, cancellationToken))
            {
                throw new ChatRuleException("room_name_taken", "A room with this name already exists.");
            }
            throw;
        }

        return new RoomSummary(room.Id, room.Name, room.Description, 1, room.CreatedAt, null);
    }

    public async Task<IReadOnlyList<RoomSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Rooms.AsNoTracking()
            .Where(room => dbContext.RoomMembers.Any(member => member.RoomId == room.Id && member.UserId == userId))
            .Select(room => new RoomRow(
                room.Id,
                room.Name,
                room.Description,
                dbContext.RoomMembers.Count(member => member.RoomId == room.Id),
                room.CreatedAt,
                dbContext.Messages
                    .Where(message => message.RoomId == room.Id && message.DeletedAt == null)
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => (Guid?)message.Id)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var lastMessages = await LastMessageLoader.LoadAsync(
            dbContext, rows.Select(row => row.LastMessageId).OfType<Guid>().ToList(), cancellationToken);

        return rows
            .Select(row => new RoomSummary(
                row.Id,
                row.Name,
                row.Description,
                row.MemberCount,
                row.CreatedAt,
                LastMessageLoader.Find(lastMessages, row.LastMessageId)))
            .OrderByDescending(room => room.LastMessage?.SentAt ?? room.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<RoomSummary>> DiscoverAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken)
    {
        var notJoined = dbContext.Rooms.AsNoTracking()
            .Where(room => !dbContext.RoomMembers.Any(member => member.RoomId == room.Id && member.UserId == userId));

        var term = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length > 0)
        {
            notJoined = notJoined.Where(room => room.Name.ToLower().Contains(term));
        }

        // Discover never shows message content: the last message is always null here.
        return await notJoined
            .OrderBy(room => room.Name)
            .Take(limit)
            .Select(room => new RoomSummary(
                room.Id,
                room.Name,
                room.Description,
                dbContext.RoomMembers.Count(member => member.RoomId == room.Id),
                room.CreatedAt,
                null))
            .ToListAsync(cancellationToken);
    }

    public async Task JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Rooms.AnyAsync(room => room.Id == roomId, cancellationToken))
        {
            throw new ChatRuleException("not_found", "Room not found.");
        }

        if (await IsMemberAsync(roomId, userId, cancellationToken))
        {
            return;
        }

        dbContext.RoomMembers.Add(RoomMember.Join(roomId, userId, RoomRole.Member, timeProvider.GetUtcNow()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent join of the same user already created the membership: that is success.
            if (await IsMemberAsync(roomId, userId, cancellationToken))
            {
                return;
            }
            throw;
        }
    }

    public async Task LeaveAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        var membership = await dbContext.RoomMembers
            .SingleOrDefaultAsync(member => member.RoomId == roomId && member.UserId == userId, cancellationToken)
            ?? throw new ChatRuleException("not_found", "Room not found.");

        if (membership.IsOwner
            && await dbContext.RoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId != userId, cancellationToken))
        {
            throw new ChatRuleException("owner_cannot_leave", "The owner cannot leave a room while other members remain.");
        }

        dbContext.RoomMembers.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The last member left (the owner alone): the empty room goes with its history.
        if (!await dbContext.RoomMembers.AnyAsync(member => member.RoomId == roomId, cancellationToken))
        {
            await dbContext.Messages.Where(message => message.RoomId == roomId).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Rooms.Where(room => room.Id == roomId).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private sealed record RoomRow(
        Guid Id, string Name, string Description, int MemberCount, DateTimeOffset CreatedAt, Guid? LastMessageId);

    private Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
        dbContext.Rooms.AnyAsync(room => room.NormalizedName == normalizedName, cancellationToken);

    private Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.RoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId == userId, cancellationToken);
}

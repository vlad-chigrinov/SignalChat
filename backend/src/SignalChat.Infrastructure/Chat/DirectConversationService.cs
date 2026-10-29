using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class DirectConversationService(SignalChatDbContext dbContext, TimeProvider timeProvider) : IDirectConversationService
{
    private const string NotFoundCode = "not_found";

    public async Task<IReadOnlyList<DirectConversationSummary>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await Project(
            ConversationsOf(userId).Where(conversation =>
                conversation.Status == DirectConversationStatus.Accepted || conversation.InitiatorId == userId),
            userId).ToListAsync(cancellationToken);

        var items = await SummarizeAsync(rows, cancellationToken);
        return items
            .OrderByDescending(item => item.Summary.LastMessage?.SentAt ?? item.Row.CreatedAt)
            .Select(item => item.Summary)
            .ToList();
    }

    public async Task<DirectRequestOutcome> RequestAsync(Guid userId, Guid peerId, string? text, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidate = DirectConversation.Request(userId, peerId, now);
        if (!await dbContext.Users.AnyAsync(user => user.Id == peerId, cancellationToken))
        {
            throw new ChatRuleException(NotFoundCode, "User not found.");
        }

        var existing = await dbContext.DirectConversations.SingleOrDefaultAsync(
            conversation => conversation.UserLowId == candidate.UserLowId && conversation.UserHighId == candidate.UserHighId,
            cancellationToken);
        if (existing is null)
        {
            return await CreateRequestAsync(candidate, peerId, text, now, cancellationToken);
        }

        if (existing.Status == DirectConversationStatus.Accepted)
        {
            return new DirectRequestOutcome(
                DirectRequestKind.AlreadyAccepted,
                await LoadSummaryAsync(userId, existing.Id, cancellationToken),
                null);
        }
        if (existing.InitiatorId == userId)
        {
            // Waiting, or declined: the same answer, so the initiator is never told about the decline.
            throw new ChatRuleException(DirectRequestErrorCodes.RequestPending, "The request is waiting for an answer.");
        }
        if (existing.Status == DirectConversationStatus.Pending)
        {
            throw new ChatRuleException(DirectRequestErrorCodes.IncomingRequestExists, "The peer has sent a request to answer first.");
        }

        // Declined by the caller: they may write first, which makes the conversation a new request from them.
        await EnsureOutgoingLimitAsync(userId, cancellationToken);
        var message = Message.InDirectConversation(existing.Id, userId, text, now);
        existing.Reopen(userId);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DirectRequestOutcome(
            DirectRequestKind.Reopened,
            await LoadSummaryAsync(userId, existing.Id, cancellationToken),
            await RequestMessageAsync(message, userId, cancellationToken));
    }

    public async Task<IReadOnlyList<IncomingDirectRequestView>> ListIncomingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await Project(
            ConversationsOf(userId).Where(conversation =>
                conversation.Status == DirectConversationStatus.Pending && conversation.InitiatorId != userId),
            userId).ToListAsync(cancellationToken);

        var items = await SummarizeAsync(rows, cancellationToken);
        return items
            .Select(item => item.Summary)
            .Where(summary => summary.LastMessage is not null)
            .OrderByDescending(summary => summary.LastMessage!.SentAt)
            .Select(summary => new IncomingDirectRequestView(
                summary.Id, summary.Peer, summary.LastMessage!.Text, summary.LastMessage.SentAt))
            .ToList();
    }

    public async Task<DirectRequestResponse> AcceptAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await FindParticipantAsync(userId, conversationId, cancellationToken);
        conversation.Accept(userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        var responder = (await LoadPeersAsync([userId], cancellationToken))[userId];
        return new DirectRequestResponse(conversation.Id, conversation.InitiatorId, responder);
    }

    public async Task DeclineAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await FindParticipantAsync(userId, conversationId, cancellationToken);
        conversation.Decline(userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DirectRequestOutcome> CreateRequestAsync(
        DirectConversation candidate, Guid peerId, string? text, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await EnsureOutgoingLimitAsync(candidate.InitiatorId, cancellationToken);
        var message = Message.InDirectConversation(candidate.Id, candidate.InitiatorId, text, now);

        dbContext.DirectConversations.Add(candidate);
        dbContext.Messages.Add(message);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The peer created the pair at the same moment: answer for the conversation that exists now.
            dbContext.ChangeTracker.Clear();
            return await RequestAsync(candidate.InitiatorId, peerId, text, cancellationToken);
        }

        return new DirectRequestOutcome(
            DirectRequestKind.Created,
            await LoadSummaryAsync(candidate.InitiatorId, candidate.Id, cancellationToken),
            await RequestMessageAsync(message, candidate.InitiatorId, cancellationToken));
    }

    private async Task EnsureOutgoingLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var waiting = await dbContext.DirectConversations.CountAsync(
            conversation => conversation.InitiatorId == userId && conversation.Status == DirectConversationStatus.Pending,
            cancellationToken);
        if (waiting >= DirectConversation.MaxOutgoingPendingPerUser)
        {
            throw new ChatRuleException(DirectRequestErrorCodes.RequestLimitReached, "Too many requests are waiting for an answer.");
        }
    }

    /// <summary>A tracked conversation the user takes part in. Anyone else gets "not_found", the same as a missing one.</summary>
    private async Task<DirectConversation> FindParticipantAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await dbContext.DirectConversations.SingleOrDefaultAsync(
            conversation => conversation.Id == conversationId
                && (conversation.UserLowId == userId || conversation.UserHighId == userId),
            cancellationToken)
        ?? throw new ChatRuleException(NotFoundCode, "Direct request not found.");

    private async Task<DirectConversationSummary> LoadSummaryAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var rows = await Project(ConversationsOf(userId).Where(conversation => conversation.Id == conversationId), userId)
            .ToListAsync(cancellationToken);
        return (await SummarizeAsync(rows, cancellationToken)).Single().Summary;
    }

    private async Task<MessageView> RequestMessageAsync(Message message, Guid authorId, CancellationToken cancellationToken)
    {
        var author = (await LoadPeersAsync([authorId], cancellationToken))[authorId];
        return new MessageView(message.Id, message.ConversationId, author, message.Text, message.CreatedAt);
    }

    /// <summary>The direct conversations the user takes part in. Filters go here, before the projection.</summary>
    private IQueryable<DirectConversation> ConversationsOf(Guid userId) =>
        dbContext.DirectConversations.AsNoTracking()
            .Where(conversation => conversation.UserLowId == userId || conversation.UserHighId == userId);

    /// <summary>
    /// The peer id and the id of the newest message, per conversation. The projection holds a subquery, so the
    /// caller's filters are applied to <see cref="ConversationsOf"/> before it.
    /// </summary>
    private IQueryable<DirectRow> Project(IQueryable<DirectConversation> conversations, Guid userId) =>
        conversations.Select(conversation => new DirectRow(
            conversation.Id,
            conversation.UserLowId == userId ? conversation.UserHighId : conversation.UserLowId,
            conversation.CreatedAt,
            conversation.Status,
            dbContext.Messages
                .Where(message => message.DirectConversationId == conversation.Id && message.DeletedAt == null)
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Select(message => (Guid?)message.Id)
                .FirstOrDefault()));

    private async Task<List<(DirectRow Row, DirectConversationSummary Summary)>> SummarizeAsync(
        IReadOnlyList<DirectRow> rows, CancellationToken cancellationToken)
    {
        var peers = await LoadPeersAsync(rows.Select(row => row.PeerId).Distinct().ToList(), cancellationToken);
        var lastMessages = await LastMessageLoader.LoadAsync(
            dbContext, rows.Select(row => row.LastMessageId).OfType<Guid>().ToList(), cancellationToken);

        return rows
            .Select(row => (Row: row, Summary: new DirectConversationSummary(
                row.Id,
                peers[row.PeerId],
                LastMessageLoader.Find(lastMessages, row.LastMessageId),
                row.Status)))
            .ToList();
    }

    private async Task<Dictionary<Guid, ChatUser>> LoadPeersAsync(IReadOnlyCollection<Guid> peerIds, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => peerIds.Contains(user.Id))
            .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
            .ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Id);
    }

    private sealed record DirectRow(
        Guid Id, Guid PeerId, DateTimeOffset CreatedAt, DirectConversationStatus Status, Guid? LastMessageId);
}

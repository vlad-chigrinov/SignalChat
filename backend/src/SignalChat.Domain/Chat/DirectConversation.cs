namespace SignalChat.Domain.Chat;

/// <summary>
/// A one-to-one conversation. The pair is stored ordered (<see cref="UserLowId"/> &lt; <see cref="UserHighId"/>),
/// so one pair of users has exactly one conversation, whichever side starts it. It starts as a request from
/// <see cref="InitiatorId"/>, and becomes a chat only when the other side accepts.
/// </summary>
public sealed class DirectConversation
{
    /// <summary>Requests one user may have waiting for an answer at the same time.</summary>
    public const int MaxOutgoingPendingPerUser = 20;

    private const string NotFoundCode = "not_found";

    private DirectConversation()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }

    public Guid UserLowId { get; private set; }

    public Guid UserHighId { get; private set; }

    /// <summary>Who started the current request. After a decline and a reopen, this is the decliner.</summary>
    public Guid InitiatorId { get; private set; }

    public DirectConversationStatus Status { get; private set; }

    /// <summary>When the request was last answered. Null while it waits for an answer.</summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>A new conversation that waits for <paramref name="peerId"/> to answer <paramref name="initiatorId"/>.</summary>
    public static DirectConversation Request(Guid initiatorId, Guid peerId, DateTimeOffset now)
    {
        if (initiatorId == peerId)
        {
            throw new ChatRuleException("self_conversation", "A direct conversation needs two different users.");
        }

        var (low, high) = initiatorId.CompareTo(peerId) < 0 ? (initiatorId, peerId) : (peerId, initiatorId);
        return new DirectConversation
        {
            Id = Guid.CreateVersion7(now),
            UserLowId = low,
            UserHighId = high,
            InitiatorId = initiatorId,
            Status = DirectConversationStatus.Pending,
            CreatedAt = now,
        };
    }

    public bool Includes(Guid userId) => userId == UserLowId || userId == UserHighId;

    /// <summary>The other participant. Throws when <paramref name="userId"/> is not a participant.</summary>
    public Guid PeerOf(Guid userId)
    {
        if (userId == UserLowId)
        {
            return UserHighId;
        }
        if (userId == UserHighId)
        {
            return UserLowId;
        }
        throw new ArgumentException("The user does not take part in this conversation.", nameof(userId));
    }

    /// <summary>The participant asked to answer: the one who is not the initiator.</summary>
    public bool IsResponder(Guid userId) => Includes(userId) && userId != InitiatorId;

    /// <summary>The responder accepts the pending request. Anyone else, or any other state, is "not_found".</summary>
    public void Accept(Guid userId, DateTimeOffset now) => Answer(userId, now, DirectConversationStatus.Accepted);

    /// <summary>The responder declines the pending request. Silent: the initiator is never told.</summary>
    public void Decline(Guid userId, DateTimeOffset now) => Answer(userId, now, DirectConversationStatus.Declined);

    /// <summary>After a decline, the decliner may write first: the conversation becomes a new request from them.</summary>
    public void Reopen(Guid userId)
    {
        if (Status != DirectConversationStatus.Declined || !IsResponder(userId))
        {
            throw new ChatRuleException(NotFoundCode, "Direct request not found.");
        }

        InitiatorId = userId;
        Status = DirectConversationStatus.Pending;
        RespondedAt = null;
    }

    private void Answer(Guid userId, DateTimeOffset now, DirectConversationStatus answer)
    {
        if (Status != DirectConversationStatus.Pending || !IsResponder(userId))
        {
            throw new ChatRuleException(NotFoundCode, "Direct request not found.");
        }

        Status = answer;
        RespondedAt = now;
    }
}

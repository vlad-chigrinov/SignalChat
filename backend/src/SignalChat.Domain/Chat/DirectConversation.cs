namespace SignalChat.Domain.Chat;

/// <summary>
/// A one-to-one conversation. The pair is stored ordered (<see cref="UserLowId"/> &lt; <see cref="UserHighId"/>),
/// so one pair of users has exactly one conversation, whichever side starts it.
/// </summary>
public sealed class DirectConversation
{
    private DirectConversation()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }

    public Guid UserLowId { get; private set; }

    public Guid UserHighId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DirectConversation Between(Guid first, Guid second, DateTimeOffset now)
    {
        if (first == second)
        {
            throw new ChatRuleException("self_conversation", "A direct conversation needs two different users.");
        }

        var (low, high) = first.CompareTo(second) < 0 ? (first, second) : (second, first);
        return new DirectConversation
        {
            Id = Guid.CreateVersion7(now),
            UserLowId = low,
            UserHighId = high,
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
}

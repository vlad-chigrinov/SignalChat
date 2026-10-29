namespace SignalChat.Domain.Chat;

/// <summary>
/// Where a direct conversation stands. A new one is a <see cref="Pending"/> request from its initiator; only an
/// <see cref="Accepted"/> conversation is a chat. <see cref="Declined"/> is final until the decliner writes first.
/// </summary>
public enum DirectConversationStatus
{
    Pending,
    Accepted,
    Declined,
}

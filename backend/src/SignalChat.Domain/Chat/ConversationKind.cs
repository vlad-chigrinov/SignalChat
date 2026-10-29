namespace SignalChat.Domain.Chat;

/// <summary>Rooms and direct conversations share one id space; the kind tells them apart.</summary>
public enum ConversationKind
{
    Room,
    Direct,
}

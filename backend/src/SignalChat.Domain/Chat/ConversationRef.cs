namespace SignalChat.Domain.Chat;

/// <summary>A resolved conversation: which kind of container an id points to.</summary>
public readonly record struct ConversationRef(ConversationKind Kind, Guid Id);

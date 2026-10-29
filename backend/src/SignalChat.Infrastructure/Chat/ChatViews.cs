namespace SignalChat.Infrastructure.Chat;

/// <summary>Public profile of a user as shown in chat. Never carries the email.</summary>
public sealed record ChatUser(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary>The newest visible message of a conversation, as the list views show it.</summary>
public sealed record LastMessageView(Guid AuthorId, string Text, DateTimeOffset SentAt);

public sealed record RoomSummary(
    Guid Id,
    string Name,
    string Description,
    int MemberCount,
    DateTimeOffset CreatedAt,
    LastMessageView? LastMessage);

public sealed record DirectConversationSummary(Guid Id, ChatUser Peer, LastMessageView? LastMessage);

public sealed record MessageView(
    Guid Id,
    Guid ConversationId,
    ChatUser Author,
    string Text,
    DateTimeOffset SentAt);

/// <summary>One history page, oldest message first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record MessagePage(IReadOnlyList<MessageView> Items, string? NextCursor);

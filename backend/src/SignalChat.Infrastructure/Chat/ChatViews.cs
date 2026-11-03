using SignalChat.Domain.Chat;

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

/// <summary>A member of a room as the member list shows them: the current public profile and the role.</summary>
public sealed record RoomMemberView(ChatUser User, RoomRole Role);

/// <summary>A user who has just become a member of a room, on their own or by invitation.</summary>
public sealed record RoomMemberAdded(ChatUser User, DateTimeOffset JoinedAt);

/// <summary>A direct conversation as its caller sees it. <see cref="Status"/> is the stored state, not the display name.</summary>
public sealed record DirectConversationSummary(
    Guid Id,
    ChatUser Peer,
    LastMessageView? LastMessage,
    DirectConversationStatus Status);

/// <summary>A request waiting for the caller's answer. <see cref="Text"/> is the request's message.</summary>
public sealed record IncomingDirectRequestView(Guid Id, ChatUser From, string Text, DateTimeOffset CreatedAt);

public enum DirectRequestKind
{
    /// <summary>No conversation existed: a new request was created.</summary>
    Created,

    /// <summary>The caller had been declined and asked again: the conversation is a new request from the caller.</summary>
    Reopened,

    /// <summary>The conversation is already accepted: nothing changed, the text was not needed.</summary>
    AlreadyAccepted,
}

/// <summary>
/// The result of asking to write to a user. <see cref="RequestMessage"/> is the request's text for the
/// recipient's live event; it is null when the conversation was already accepted.
/// </summary>
public sealed record DirectRequestOutcome(
    DirectRequestKind Kind,
    DirectConversationSummary Conversation,
    MessageView? RequestMessage);

/// <summary>The answer to an accepted request: the initiator learns who accepted.</summary>
public sealed record DirectRequestResponse(Guid ConversationId, Guid InitiatorId, ChatUser Responder);

public sealed record MessageView(
    Guid Id,
    Guid ConversationId,
    ChatUser Author,
    string Text,
    DateTimeOffset SentAt)
{
    /// <summary>Counts per emoji as the viewer sees them; empty for a message that was just written.</summary>
    public IReadOnlyList<ReactionView> Reactions { get; init; } = [];
}

/// <summary>One history page, oldest message first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record MessagePage(IReadOnlyList<MessageView> Items, string? NextCursor);

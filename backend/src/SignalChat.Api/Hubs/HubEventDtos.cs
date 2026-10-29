using SignalChat.Api.Chat.Dto;

namespace SignalChat.Api.Hubs;

/// <summary>A user joined or left a conversation (sent to the other members of its group).</summary>
public sealed record ConversationMemberEventDto(Guid ConversationId, UserSummaryDto User);

/// <summary>A user is typing in a conversation. There is no "stopped typing" event: the client hides the hint on a timer.</summary>
public sealed record UserTypingEventDto(Guid ConversationId, Guid UserId);

/// <summary>A user with a shared conversation went online or offline.</summary>
public sealed record PresenceChangedDto(Guid UserId, bool IsOnline);

/// <summary>A direct request reached its recipient. Sent to the recipient's user group, once, when the request is created.</summary>
public sealed record DirectRequestReceivedDto(Guid ConversationId, UserSummaryDto From, string Text, DateTimeOffset CreatedAt);

/// <summary>The peer accepted the request. Sent to the initiator's user group; the initiator then calls JoinRoom for the conversation.</summary>
public sealed record DirectRequestAcceptedDto(Guid ConversationId, UserSummaryDto Peer);

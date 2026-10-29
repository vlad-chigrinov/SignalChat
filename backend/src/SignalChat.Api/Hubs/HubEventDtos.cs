using SignalChat.Api.Chat.Dto;

namespace SignalChat.Api.Hubs;

/// <summary>A user joined or left a conversation (sent to the other members of its group).</summary>
public sealed record ConversationMemberEventDto(Guid ConversationId, UserSummaryDto User);

/// <summary>A user is typing in a conversation. There is no "stopped typing" event: the client hides the hint on a timer.</summary>
public sealed record UserTypingEventDto(Guid ConversationId, Guid UserId);

/// <summary>A user with a shared conversation went online or offline.</summary>
public sealed record PresenceChangedDto(Guid UserId, bool IsOnline);

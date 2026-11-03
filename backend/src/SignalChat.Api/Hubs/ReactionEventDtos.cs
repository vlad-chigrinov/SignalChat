using SignalChat.Api.Chat.Dto;

namespace SignalChat.Api.Hubs;

/// <summary>How many people chose an emoji. No flag for "me": the same event goes to every member, so it cannot carry one.</summary>
public sealed record ReactionCountDto(string Emoji, int Count);

/// <summary>
/// The reactions of a message changed. Sent to the whole conversation group with the complete new counts (not a
/// delta), so a missed or repeated event cannot leave a wrong number. Names no user.
/// </summary>
public sealed record ReactionsChangedEventDto(Guid ConversationId, Guid MessageId, IReadOnlyList<ReactionCountDto> Reactions);

/// <summary>
/// Sent to the acting user's own connections only: which emoji they hold on the message now (null: none). Lets
/// their other tabs mark the reaction as theirs without any event telling who reacted.
/// </summary>
public sealed record MyReactionChangedEventDto(Guid ConversationId, Guid MessageId, string? Emoji);

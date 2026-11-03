namespace SignalChat.Api.Chat.Dto;

/// <summary>One emoji under a message as the caller sees it. Carries no user: only how many chose it and whether the caller did.</summary>
public sealed record ReactionDto(string Emoji, int Count, bool ReactedByMe);

/// <summary>The body of the reaction route. A null or unknown emoji is refused with "invalid_reaction".</summary>
public sealed record ChooseReactionRequest(string? Emoji);

/// <summary>The reactions of one message after the caller's choice.</summary>
public sealed record MessageReactionsDto(Guid MessageId, IReadOnlyList<ReactionDto> Reactions);

namespace SignalChat.Api.Chat.Dto;

/// <summary>
/// A message. <see cref="Reactions"/> are counts per emoji with the viewer's own marked; a message that was just
/// written (the live event) has none.
/// </summary>
public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    UserSummaryDto Author,
    string Text,
    DateTimeOffset SentAt,
    IReadOnlyList<ReactionDto> Reactions);

/// <summary>One history page, oldest first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, string? NextCursor);

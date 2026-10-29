namespace SignalChat.Api.Chat.Dto;

public sealed record MessageDto(Guid Id, Guid ConversationId, UserSummaryDto Author, string Text, DateTimeOffset SentAt);

/// <summary>One history page, oldest first. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, string? NextCursor);

namespace SignalChat.Api.Chat.Dto;

/// <summary>A user as chat shows them. Deliberately without email (email lives only in /auth/me).</summary>
public sealed record UserSummaryDto(Guid Id, string DisplayName, string? AvatarUrl);

public sealed record LastMessageDto(Guid AuthorId, string Text, DateTimeOffset SentAt);

namespace SignalChat.Api.Chat.Dto;

/// <summary>A user as chat shows them. Deliberately without email (email lives only in /auth/me).</summary>
public sealed record UserSummaryDto(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary><see cref="PhotoCount"/> is the number of photos of the message, 0 for a text; the text is then empty or the caption.</summary>
public sealed record LastMessageDto(Guid AuthorId, string Text, DateTimeOffset SentAt, int PhotoCount);

namespace SignalChat.Infrastructure.Users;

/// <summary>Identity claims Google returned in a validated ID token.</summary>
public sealed record GoogleProfile(string Subject, string Email, string? Name, string? PictureUrl);

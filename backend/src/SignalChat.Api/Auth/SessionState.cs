namespace SignalChat.Api.Auth;

/// <summary>What a session cookie says about its owner, as read back from its claims.</summary>
public readonly record struct SessionState(Guid UserId, int SessionVersion, DateTimeOffset StartedAt);

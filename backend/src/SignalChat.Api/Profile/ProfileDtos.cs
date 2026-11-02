namespace SignalChat.Api.Profile;

/// <summary>Body of <c>PUT /api/me/display-name</c>. A missing name is the same refusal as an empty one.</summary>
public sealed record ChangeDisplayNameRequest(string? DisplayName);

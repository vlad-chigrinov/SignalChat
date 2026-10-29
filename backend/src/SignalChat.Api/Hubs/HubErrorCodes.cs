namespace SignalChat.Api.Hubs;

/// <summary>
/// Stable error codes a hub client receives as the message of a <c>HubException</c>. Anything else is masked by SignalR.
/// </summary>
public static class HubErrorCodes
{
    /// <summary>Missing conversation, or the caller is not a member. Deliberately the same answer for both.</summary>
    public const string NotFound = "not_found";

    public const string InvalidMessage = "invalid_message";
    public const string RateLimited = "rate_limited";
    public const string TooManyGroups = "too_many_groups";
    public const string Unauthenticated = "unauthenticated";
}

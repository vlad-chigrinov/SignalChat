namespace SignalChat.Api.Auth;

/// <summary>Lifetimes and claim names of the session cookie.</summary>
public static class SessionPolicy
{
    /// <summary>Sliding window: the cookie is renewed on activity, up to <see cref="AbsoluteSessionLifetime"/>.</summary>
    public static readonly TimeSpan SlidingSessionLifetime = TimeSpan.FromDays(14);

    /// <summary>Hard cap counted from sign-in: a stolen cookie cannot be renewed forever.</summary>
    public static readonly TimeSpan AbsoluteSessionLifetime = TimeSpan.FromDays(30);

    /// <summary>Claim holding the <c>User.SessionVersion</c> the cookie was issued at.</summary>
    public const string SessionVersionClaim = "sv";

    /// <summary>Claim holding the sign-in time (Unix seconds), the start of the absolute lifetime.</summary>
    public const string SessionStartedClaim = "sat";
}

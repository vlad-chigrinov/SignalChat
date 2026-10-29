using Microsoft.AspNetCore.Http;
using SignalChat.Api.Auth;

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>Counter keys: the session user, or the client address for a request that has no session.</summary>
public static class ChatRateLimitKey
{
    private const string UserPrefix = "user:";
    private const string AddressPrefix = "ip:";
    private const string UnknownAddress = "unknown";

    /// <summary>The session user when there is one, otherwise the client address.</summary>
    public static string Of(HttpContext http) =>
        SessionPrincipal.TryGetUserId(http.User, out var userId)
            ? UserPrefix + userId
            : OfAddress(http);

    public static string OfAddress(HttpContext http) =>
        AddressPrefix + (http.Connection.RemoteIpAddress?.ToString() ?? UnknownAddress);
}

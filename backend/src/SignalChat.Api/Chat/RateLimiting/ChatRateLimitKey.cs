using Microsoft.AspNetCore.Http;
using SignalChat.Api.Auth;

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>The counter key: the session user, or the client address for an anonymous request.</summary>
public static class ChatRateLimitKey
{
    private const string UserPrefix = "user:";
    private const string AddressPrefix = "ip:";
    private const string UnknownAddress = "unknown";

    public static string Of(HttpContext http) =>
        SessionPrincipal.TryGetUserId(http.User, out var userId)
            ? UserPrefix + userId
            : AddressPrefix + (http.Connection.RemoteIpAddress?.ToString() ?? UnknownAddress);
}

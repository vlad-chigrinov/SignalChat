using Microsoft.Net.Http.Headers;
using SignalChat.Api.Configuration;

namespace SignalChat.Api.Security;

/// <summary>
/// CSRF / cross-site WebSocket hijacking barrier. A state-changing request or a WebSocket handshake
/// whose <c>Origin</c> is not in the allowlist is refused with 403. Browsers always send <c>Origin</c>
/// on these requests; a request without it comes from a non-browser client that has no victim cookie to ride.
/// </summary>
public sealed class OriginGuardMiddleware(RequestDelegate next, FrontendSettings frontend)
{
    private const string WebSocketUpgrade = "websocket";

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!NeedsOriginCheck(request))
        {
            return next(context);
        }

        var origin = request.Headers.Origin.ToString();
        if (origin.Length == 0 || frontend.IsAllowedOrigin(origin) || IsSameOrigin(request, origin))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool NeedsOriginCheck(HttpRequest request) =>
        !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        || request.Headers[HeaderNames.Upgrade].ToString().Contains(WebSocketUpgrade, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrigin(HttpRequest request, string origin) =>
        string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
}

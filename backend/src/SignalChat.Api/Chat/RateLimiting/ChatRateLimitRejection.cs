using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using SignalChat.Api.Hubs;

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>
/// Writes the 429 answer: a <c>Retry-After</c> header in whole seconds, and ProblemDetails with the same
/// <c>code</c> the hub uses for its rate limit, so the client handles both transports alike.
/// </summary>
public static class ChatRateLimitRejection
{
    private const string CodeExtension = "code";
    private const string ProblemTitle = "Too many requests.";
    private const string ProblemDetail = "The request rate limit for this route was exceeded. Retry after Retry-After seconds.";

    public static async ValueTask WriteAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter =
                ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(
            title: ProblemTitle,
            detail: ProblemDetail,
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> { [CodeExtension] = HubErrorCodes.RateLimited })
            .ExecuteAsync(http);
    }
}

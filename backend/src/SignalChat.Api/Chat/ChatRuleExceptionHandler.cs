using Microsoft.AspNetCore.Diagnostics;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Chat;

/// <summary>
/// Answers every <see cref="ChatRuleException"/> raised by a REST request with ProblemDetails:
/// the status from <see cref="ChatErrorStatus"/> and the stable error code in <c>code</c>.
/// </summary>
public sealed class ChatRuleExceptionHandler : IExceptionHandler
{
    private const string CodeExtension = "code";
    private const string ProblemTitle = "Chat request was refused.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ChatRuleException rule)
        {
            return false;
        }

        var status = ChatErrorStatus.For(rule.Code);
        await Results.Problem(
            title: ProblemTitle,
            detail: rule.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { [CodeExtension] = rule.Code })
            .ExecuteAsync(httpContext);
        return true;
    }
}

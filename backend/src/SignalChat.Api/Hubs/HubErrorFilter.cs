using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Turns a <see cref="ChatRuleException"/> from a hub method into a <see cref="HubException"/> with the same code.
/// Any other exception is logged with its details here and re-thrown, so SignalR masks it for the client.
/// </summary>
public sealed class HubErrorFilter(ILogger<HubErrorFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (HubException)
        {
            throw;
        }
        catch (ChatRuleException rule)
        {
            throw HubErrors.From(rule);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hub method {Method} failed for connection {ConnectionId}",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId);
            throw;
        }
    }
}

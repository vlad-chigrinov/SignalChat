using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Api.Chat.Dto;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// The send path of the hub. Strict order: membership (not_found) → rate limit (rate_limited) → save to the
/// database (validation happens inside) → broadcast to the group. Nothing is broadcast before it is stored.
/// </summary>
public sealed class ChatMessaging(
    IConversationAccess access,
    IMessageRateLimiter rateLimiter,
    IMessageStore messages,
    IHubContext<ChatHub, IChatClient> hub)
{
    public async Task<MessageDto> SendAsync(Guid userId, Guid conversationId, string? text, CancellationToken cancellationToken)
    {
        var conversation = await access.FindForMemberAsync(userId, conversationId, cancellationToken)
            ?? throw HubErrors.NotFound();

        if (!rateLimiter.TryAcquire(userId))
        {
            throw HubErrors.Of(HubErrorCodes.RateLimited);
        }

        var stored = await messages.AppendAsync(userId, conversationId, text, cancellationToken);
        var message = ChatMapping.ToDto(stored);
        await hub.Clients.Group(ChatGroups.For(conversation)).MessageReceived(message);
        return message;
    }
}

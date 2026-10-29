using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Api.Chat.Dto;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// The send path of the hub. Strict order: rate limit (rate_limited) → membership (not_found) → save to the
/// database (text validation happens inside, its <see cref="ChatRuleException"/> becomes a hub error code) →
/// broadcast to the group. Nothing is broadcast before it is stored. The limit goes first, so an outsider probing
/// foreign ids spends the rate limit instead of reaching the database for free.
/// </summary>
public sealed class ChatMessaging(
    IConversationAccess access,
    IMessageRateLimiter rateLimiter,
    IMessageStore messages,
    IHubContext<ChatHub, IChatClient> hub)
{
    public async Task<MessageDto> SendAsync(Guid userId, Guid conversationId, string? text, CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquire(userId))
        {
            throw HubErrors.Of(HubErrorCodes.RateLimited);
        }

        var conversation = await access.FindActiveAsync(userId, conversationId, cancellationToken)
            ?? throw await NotActiveAsync(userId, conversationId, cancellationToken);

        MessageView stored;
        try
        {
            stored = await messages.AppendAsync(userId, conversationId, text, cancellationToken);
        }
        catch (ChatRuleException rule)
        {
            throw HubErrors.From(rule);
        }

        var message = ChatMapping.ToDto(stored);
        await hub.Clients.Group(ChatGroups.For(conversation)).MessageReceived(message);
        return message;
    }

    /// <summary>
    /// The initiator of a pending request can see it but not write to it: "dm_not_accepted". Everyone else who
    /// is not active gets "not_found", the same answer as for a missing conversation.
    /// </summary>
    private async Task<HubException> NotActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await access.FindForMemberAsync(userId, conversationId, cancellationToken) is not null
            ? HubErrors.Of(DirectRequestErrorCodes.NotAccepted)
            : HubErrors.NotFound();
}

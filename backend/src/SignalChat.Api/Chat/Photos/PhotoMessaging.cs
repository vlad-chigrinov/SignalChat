using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Hubs;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat.Photos;

/// <summary>
/// The send path of a photo message over REST, in the order of the hub's: rate limit (the same allowance as text messages,
/// on top of the route's own) → membership and the files → saved → broadcast to the conversation. Nothing is broadcast
/// before it is stored, and the members get the same <c>MessageReceived</c> event as for a text.
/// </summary>
public sealed class PhotoMessaging(
    IMessageRateLimiter rateLimiter,
    IPhotoMessageService photos,
    IHubContext<ChatHub, IChatClient> hub)
{
    internal async Task<MessageDto> SendAsync(
        Guid userId, Guid conversationId, PhotoUploadForm form, CancellationToken cancellationToken)
    {
        if (!rateLimiter.TryAcquire(userId))
        {
            throw new ChatRuleException(HubErrorCodes.RateLimited, "Too many messages. Slow down.");
        }

        var caption = await form.ReadCaptionAsync(cancellationToken);
        var sent = await photos.SendAsync(userId, conversationId, caption, form.ReadPhotosAsync(cancellationToken), cancellationToken);

        var message = ChatMapping.ToDto(sent.Message);
        await hub.Clients.Group(ChatGroups.For(sent.Conversation)).MessageReceived(message);
        return message;
    }
}

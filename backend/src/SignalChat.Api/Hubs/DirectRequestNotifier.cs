using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Live events of the consent flow, sent to the personal groups. Called by the REST endpoints after the change is
/// stored. There is deliberately no event for a decline: the initiator learns of it only by the request staying pending.
/// </summary>
public sealed class DirectRequestNotifier(IHubContext<ChatHub, IChatClient> hub)
{
    public Task RequestReceivedAsync(Guid recipientId, MessageView request) =>
        hub.Clients.Group(ChatGroups.User(recipientId)).DirectRequestReceived(new DirectRequestReceivedDto(
            request.ConversationId,
            ChatMapping.ToDto(request.Author),
            request.Text,
            request.SentAt));

    public Task RequestAcceptedAsync(DirectRequestResponse response) =>
        hub.Clients.Group(ChatGroups.User(response.InitiatorId)).DirectRequestAccepted(new DirectRequestAcceptedDto(
            response.ConversationId,
            ChatMapping.ToDto(response.Responder)));
}

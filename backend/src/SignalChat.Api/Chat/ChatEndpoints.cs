using Microsoft.AspNetCore.Routing;
using SignalChat.Api.Chat.Photos;
using SignalChat.Api.Profile;

namespace SignalChat.Api.Chat;

/// <summary>All chat REST routes live under /api and need a session. Text messages are sent through the hub; photo messages through REST (multipart).</summary>
public static class ChatEndpoints
{
    public const string ApiPrefix = "/api";

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(ApiPrefix).RequireAuthorization();
        api.MapRoomEndpoints();
        api.MapDirectConversationEndpoints();
        api.MapMessageEndpoints();
        api.MapPhotoMessageEndpoints();
        api.MapAttachmentEndpoints();
        api.MapUserSearchEndpoints();
        api.MapPresenceEndpoints();
        api.MapProfileEndpoints();
        return endpoints;
    }
}

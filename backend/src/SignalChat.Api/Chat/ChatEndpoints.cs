using Microsoft.AspNetCore.Routing;
using SignalChat.Api.Profile;

namespace SignalChat.Api.Chat;

/// <summary>All chat REST routes live under /api and need a session. Messages are sent through the hub only.</summary>
public static class ChatEndpoints
{
    public const string ApiPrefix = "/api";

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(ApiPrefix).RequireAuthorization();
        api.MapRoomEndpoints();
        api.MapRoomMemberEndpoints();
        api.MapDirectConversationEndpoints();
        api.MapMessageEndpoints();
        api.MapReactionEndpoints();
        api.MapUserSearchEndpoints();
        api.MapPresenceEndpoints();
        api.MapProfileEndpoints();
        return endpoints;
    }
}

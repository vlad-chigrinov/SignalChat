using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

public static class DirectConversationEndpoints
{
    private const string DirectGroup = "/direct-conversations";

    public static RouteGroupBuilder MapDirectConversationEndpoints(this RouteGroupBuilder api)
    {
        var direct = api.MapGroup(DirectGroup);
        direct.MapGet("/", ListMineAsync);
        direct.MapPost("/", GetOrCreateAsync).RequireRateLimiting(ChatRateLimitPolicies.DirectConversationCreate);
        return api;
    }

    private static async Task<IResult> ListMineAsync(
        ClaimsPrincipal user, IDirectConversationService conversations, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var items = await conversations.ListMineAsync(userId, cancellationToken);
        return Results.Ok(items.Select(conversation => ChatMapping.ToDto(conversation)).ToList());
    }

    private static async Task<IResult> GetOrCreateAsync(
        ClaimsPrincipal user, CreateDirectConversationRequest request, IDirectConversationService conversations,
        CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        // Idempotent: the existing conversation is returned, a new one is created only on first use.
        var conversation = await conversations.GetOrCreateAsync(userId, request.UserId, cancellationToken);
        return Results.Ok(ChatMapping.ToDto(conversation));
    }
}

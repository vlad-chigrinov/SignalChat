using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Api.Hubs;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

public static class DirectConversationEndpoints
{
    private const string DirectGroup = "/direct-conversations";
    private const string RequestGroup = "/direct-requests";

    public static RouteGroupBuilder MapDirectConversationEndpoints(this RouteGroupBuilder api)
    {
        var direct = api.MapGroup(DirectGroup);
        direct.MapGet("/", ListMineAsync);
        direct.MapPost("/", RequestAsync).RequireRateLimiting(ChatRateLimitPolicies.DirectConversationCreate);
        direct.MapPost("/{id:guid}/accept", AcceptAsync);
        direct.MapPost("/{id:guid}/decline", DeclineAsync);

        var requests = api.MapGroup(RequestGroup);
        requests.MapGet("/incoming", IncomingAsync);
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

    /// <summary>
    /// Creates a request (201), reports an accepted conversation (200), or reopens a request the caller was declined on (201).
    /// A new request reaches the recipient live at once.
    /// </summary>
    private static async Task<IResult> RequestAsync(
        ClaimsPrincipal user, CreateDirectConversationRequest request, IDirectConversationService conversations,
        DirectRequestNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var outcome = await conversations.RequestAsync(userId, request.UserId, request.Text, cancellationToken);
        var dto = ChatMapping.ToDto(outcome.Conversation);
        if (outcome.RequestMessage is { } requestMessage)
        {
            await notifier.RequestReceivedAsync(request.UserId, requestMessage);
            return Results.Json(dto, statusCode: StatusCodes.Status201Created);
        }
        return Results.Ok(dto);
    }

    private static async Task<IResult> IncomingAsync(
        ClaimsPrincipal user, IDirectConversationService conversations, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var items = await conversations.ListIncomingAsync(userId, cancellationToken);
        return Results.Ok(items.Select(request => ChatMapping.ToDto(request)).ToList());
    }

    private static async Task<IResult> AcceptAsync(
        ClaimsPrincipal user, Guid id, IDirectConversationService conversations, DirectRequestNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var response = await conversations.AcceptAsync(userId, id, cancellationToken);
        await notifier.RequestAcceptedAsync(response);
        return Results.NoContent();
    }

    /// <summary>Silent: the initiator gets no event and only sees the request still pending.</summary>
    private static async Task<IResult> DeclineAsync(
        ClaimsPrincipal user, Guid id, IDirectConversationService conversations, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        await conversations.DeclineAsync(userId, id, cancellationToken);
        return Results.NoContent();
    }
}

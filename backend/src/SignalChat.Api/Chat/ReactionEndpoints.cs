using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Api.Hubs;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

/// <summary>
/// The caller picks a reaction for a message of a conversation they can write in. One route for all three moves, so the
/// server decides which it is: first pick adds, another emoji replaces, the same emoji again removes.
/// </summary>
public static class ReactionEndpoints
{
    public static RouteGroupBuilder MapReactionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/conversations/{id:guid}/messages/{messageId:guid}/reaction", ChooseAsync)
            .RequireRateLimiting(ChatRateLimitPolicies.Reaction);
        return api;
    }

    private static async Task<IResult> ChooseAsync(
        ClaimsPrincipal user, Guid id, Guid messageId, ChooseReactionRequest request,
        IReactionStore reactions, ReactionNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var change = await reactions.ChooseAsync(userId, id, messageId, request.Emoji, cancellationToken);
        await notifier.NotifyAsync(userId, change, cancellationToken);
        return Results.Ok(new MessageReactionsDto(change.MessageId, ChatMapping.ToDtos(change.Reactions)));
    }
}

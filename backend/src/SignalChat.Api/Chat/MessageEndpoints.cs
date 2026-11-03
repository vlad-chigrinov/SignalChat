using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

/// <summary>Read-only history. There is deliberately no POST route: messages are sent through the hub (#6).</summary>
public static class MessageEndpoints
{
    public static RouteGroupBuilder MapMessageEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/conversations/{id:guid}/messages", GetPageAsync);
        return api;
    }

    private static async Task<IResult> GetPageAsync(
        ClaimsPrincipal user, Guid id, int? limit, string? before,
        IConversationAccess access, IMessageStore messages, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        // A non-member and a missing conversation get the same answer: no way to probe ids.
        var conversation = await access.FindForMemberAsync(userId, id, cancellationToken)
            ?? throw new ChatRuleException("not_found", "Conversation not found.");

        var pageSize = Pagination.Clamp(limit, ChatPaging.DefaultPageSize, ChatPaging.MaxPageSize);
        var cursor = ParseCursor(before);
        var page = await messages.GetPageAsync(userId, conversation, cursor, pageSize, cancellationToken);
        return Results.Ok(ChatMapping.ToDto(page));
    }

    private static MessageCursor? ParseCursor(string? before)
    {
        if (before is null)
        {
            return null;
        }
        if (!MessageCursor.TryParse(before, out var cursor))
        {
            throw new ChatRuleException("invalid_cursor", "The before cursor is malformed.");
        }
        return cursor;
    }
}

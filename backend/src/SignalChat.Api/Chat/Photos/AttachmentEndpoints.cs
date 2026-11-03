using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Media;

namespace SignalChat.Api.Chat.Photos;

/// <summary>
/// <c>GET /api/attachments/{id}/{original|thumbnail}</c>. The API streams the file out of the object store after it has
/// checked that the caller may see the conversation of the message; the store is never reachable from outside, and a
/// photo has no address that works without a session. Add <c>?download=true</c> to get it as a file under its name.
/// </summary>
public static class AttachmentEndpoints
{
    // A stored photo never changes (a new one is a new id), so the browser may keep it. "private": never in a shared cache.
    private const string ImmutableForAYear = "private, max-age=31536000, immutable";

    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/attachments/{attachmentId:guid}/{variant}", GetAsync);
        return api;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user, Guid attachmentId, string variant, bool? download, HttpContext http,
        IAttachmentReader attachments, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }
        if (!AttachmentRoutes.TryParseVariant(variant, out var kind))
        {
            return Results.NotFound();
        }

        AttachmentFile? file;
        try
        {
            file = await attachments.OpenAsync(userId, attachmentId, kind, cancellationToken);
        }
        catch (MediaStorageException)
        {
            throw new ChatRuleException(AttachmentErrorCodes.StorageUnavailable, "The photo could not be read. Try again later.");
        }
        if (file is null)
        {
            return Results.NotFound();
        }

        http.Response.RegisterForDisposeAsync(file.Content);
        http.Response.Headers.CacheControl = ImmutableForAYear;
        http.Response.ContentLength = file.Content.Length;
        return download == true
            ? Results.File(file.Content.Content, file.ContentType, file.FileName)
            : Results.Stream(file.Content.Content, file.ContentType);
    }
}

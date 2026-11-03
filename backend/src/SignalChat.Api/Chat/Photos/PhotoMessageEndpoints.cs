using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Media;

namespace SignalChat.Api.Chat.Photos;

/// <summary>
/// <c>POST /api/conversations/{id}/messages/photos</c>: a message of 1..9 photos with an optional caption, as multipart
/// form data (<c>text</c> first, then <c>files</c>, one part per photo). The user is the one in the session.
/// </summary>
public static class PhotoMessageEndpoints
{
    public static IServiceCollection AddPhotoMessages(this IServiceCollection services) =>
        services.AddScoped<PhotoMessaging>();

    public static RouteGroupBuilder MapPhotoMessageEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/conversations/{id:guid}/messages/photos", SendAsync).RequireRateLimiting(ChatRateLimitPolicies.PhotoUpload);
        return api;
    }

    private static async Task<IResult> SendAsync(
        ClaimsPrincipal user, Guid id, HttpContext http, PhotoMessaging messaging, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var bodyLimit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false })
        {
            bodyLimit.MaxRequestBodySize = PhotoLimits.MaxRequestBodyBytes;
        }

        var form = PhotoUploadForm.Open(http.Request);
        try
        {
            var message = await messaging.SendAsync(userId, id, form, cancellationToken);
            return Results.Json(message, statusCode: StatusCodes.Status201Created);
        }
        catch (BadHttpRequestException tooLarge) when (tooLarge.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw new ChatRuleException(AttachmentErrorCodes.AttachmentTooLarge, "The photos are too large.");
        }
        catch (InvalidDataException malformed)
        {
            // The multipart reader's own refusal: a broken body, or a part whose headers are over its limit.
            throw new ChatRuleException(AttachmentErrorCodes.InvalidAttachments, malformed.Message);
        }
    }
}

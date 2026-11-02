using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Profile;

/// <summary>
/// The signed-in user edits their own name and avatar. The routes carry no user id: the user is always the one in
/// the session, so no one can change another person's profile. Avatars are read through <c>GET /users/{id}/avatar</c>.
/// </summary>
public static class ProfileEndpoints
{
    private const string MeGroup = "/me";
    private const string ImmutableForAYear = "private, max-age=31536000, immutable";

    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder api)
    {
        var me = api.MapGroup(MeGroup).RequireRateLimiting(ChatRateLimitPolicies.ProfileUpdate);
        me.MapPut("/display-name", ChangeDisplayNameAsync);
        me.MapPut("/avatar", UploadAvatarAsync);
        me.MapDelete("/avatar", ResetAvatarAsync);

        api.MapGet("/users/{userId:guid}/avatar", GetAvatarAsync);
        return api;
    }

    private static async Task<IResult> ChangeDisplayNameAsync(
        ClaimsPrincipal principal, ChangeDisplayNameRequest request, IProfileService profiles,
        ProfileChangeNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await profiles.ChangeDisplayNameAsync(userId, request.DisplayName, cancellationToken);
        return await RespondAsync(user, notifier, cancellationToken);
    }

    /// <summary>
    /// The body is the picture itself. Its type is decided from the content and the stored file is a re-encoded copy,
    /// so the media type the client sends does not matter.
    /// </summary>
    private static async Task<IResult> UploadAvatarAsync(
        ClaimsPrincipal principal, HttpContext http, IProfileService profiles,
        ProfileChangeNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var bodyLimit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false })
        {
            bodyLimit.MaxRequestBodySize = AvatarLimits.MaxUploadBytes + 1;
        }

        var upload = await BoundedBody.ReadAsync(http.Request.Body, AvatarLimits.MaxUploadBytes, cancellationToken);
        var user = await profiles.UploadAvatarAsync(userId, upload, cancellationToken);
        return await RespondAsync(user, notifier, cancellationToken);
    }

    private static async Task<IResult> ResetAvatarAsync(
        ClaimsPrincipal principal, IProfileService profiles, ProfileChangeNotifier notifier, CancellationToken cancellationToken)
    {
        if (!SessionPrincipal.TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await profiles.ResetAvatarAsync(userId, cancellationToken);
        return await RespondAsync(user, notifier, cancellationToken);
    }

    /// <summary>
    /// Any signed-in user may read any avatar: names and avatars are public inside the chat (the search lists every user).
    /// The address carries the version, so a cached copy is never stale and can be kept for a year.
    /// </summary>
    private static async Task<IResult> GetAvatarAsync(
        Guid userId, HttpContext http, IProfileService profiles, CancellationToken cancellationToken)
    {
        var image = await profiles.FindAvatarImageAsync(userId, cancellationToken);
        if (image is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = ImmutableForAYear;
        return Results.File(image, AvatarLimits.StoredContentType);
    }

    private static async Task<IResult> RespondAsync(User user, ProfileChangeNotifier notifier, CancellationToken cancellationToken)
    {
        await notifier.NotifyAsync(user, cancellationToken);
        return Results.Ok(CurrentUserResponse.From(user));
    }
}

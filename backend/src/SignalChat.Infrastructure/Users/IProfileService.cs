using SignalChat.Domain.Users;

namespace SignalChat.Infrastructure.Users;

/// <summary>
/// The profile a user edits themselves: name and avatar. Every method acts on the given user only; the caller
/// passes the id of the signed-in user and never an id taken from the request.
/// </summary>
public interface IProfileService
{
    /// <summary>Throws <c>ChatRuleException</c> <c>invalid_display_name</c> for a name the rules refuse.</summary>
    Task<User> ChangeDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken);

    /// <summary>Stores the upload as the user's avatar after re-encoding it. Throws <c>invalid_avatar</c> or <c>avatar_too_large</c>.</summary>
    Task<User> UploadAvatarAsync(Guid userId, byte[] upload, CancellationToken cancellationToken);

    /// <summary>Drops the uploaded picture; the avatar is the one from Google again.</summary>
    Task<User> ResetAvatarAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The stored avatar bytes (WebP), or null when the user has no uploaded avatar.</summary>
    Task<byte[]?> FindAvatarImageAsync(Guid userId, CancellationToken cancellationToken);
}

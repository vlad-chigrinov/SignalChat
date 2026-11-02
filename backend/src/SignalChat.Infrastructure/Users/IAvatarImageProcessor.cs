namespace SignalChat.Infrastructure.Users;

public interface IAvatarImageProcessor
{
    /// <summary>
    /// Turns an upload into the stored avatar: a square <see cref="AvatarLimits.OutputSidePx"/> WebP made of the decoded
    /// pixels alone. The type is decided by the content, never by a name or a media type. Throws
    /// <c>ChatRuleException</c> with <c>invalid_avatar</c> or <c>avatar_too_large</c>.
    /// </summary>
    byte[] Process(byte[] upload);
}

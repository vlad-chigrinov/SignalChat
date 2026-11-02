namespace SignalChat.Domain.Users;

/// <summary>Stable codes of the profile rules. They travel as <c>ChatRuleException.Code</c>, so the REST answer carries them.</summary>
public static class UserProfileErrorCodes
{
    public const string InvalidDisplayName = "invalid_display_name";

    /// <summary>The upload is not a usable picture: wrong content, damaged, or larger than the pixel limits.</summary>
    public const string InvalidAvatar = "invalid_avatar";

    public const string AvatarTooLarge = "avatar_too_large";
}

namespace SignalChat.Domain.Users;

/// <summary>
/// A person signed in through Google. <see cref="DisplayName"/> and <see cref="AvatarUrl"/> form the
/// public chat profile; <see cref="Email"/> is private and encrypted at rest by the persistence layer.
/// </summary>
public sealed class User
{
    public const int GoogleSubjectMaxLength = 255;
    public const int DisplayNameMaxLength = 100;
    public const int AvatarUrlMaxLength = 2048;
    public const string AllowedAvatarHostSuffix = ".googleusercontent.com";
    public const string FallbackDisplayName = "SignalChat user";
    public const int InitialSessionVersion = 1;

    private User()
    {
        // Required by EF Core.
        GoogleSubject = Email = DisplayName = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable Google account identifier (the OIDC <c>sub</c> claim).</summary>
    public string GoogleSubject { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>
    /// The picture chat shows: the uploaded one (<see cref="UploadedAvatarAddress"/>) or, without it, the Google one.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>The picture Google sent on the latest sign-in; what <see cref="UseGoogleAvatar"/> returns to.</summary>
    public string? GoogleAvatarUrl { get; private set; }

    /// <summary>True once the user chose a name; Google's name then no longer replaces it on sign-in.</summary>
    public bool HasCustomDisplayName { get; private set; }

    /// <summary>True while the user's own uploaded picture is the avatar.</summary>
    public bool HasCustomAvatar { get; private set; }

    /// <summary>Counts uploads. It is part of the avatar address, so every upload is a new address and caches let go.</summary>
    public int AvatarVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastLoginAt { get; private set; }

    /// <summary>
    /// Every session cookie carries the version it was issued at. Bumping it invalidates all of
    /// this user's sessions at once, on every device.
    /// </summary>
    public int SessionVersion { get; private set; } = InitialSessionVersion;

    /// <summary>Revokes every existing session of this user (logout, suspected cookie theft).</summary>
    public void RevokeSessions() => SessionVersion++;

    public static User RegisterFromGoogle(
        string googleSubject, string email, string? displayName, string? avatarUrl, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSubject);
        if (googleSubject.Length > GoogleSubjectMaxLength)
        {
            throw new ArgumentException("Google subject is too long.", nameof(googleSubject));
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            GoogleSubject = googleSubject,
            CreatedAt = now,
        };
        user.RecordLogin(email, displayName, avatarUrl, now);
        return user;
    }

    /// <summary>Refreshes the profile with what Google returned on this sign-in.</summary>
    public void RecordLogin(string email, string? displayName, string? avatarUrl, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Email = email.Trim();
        if (!HasCustomDisplayName)
        {
            DisplayName = NormalizeDisplayName(displayName);
        }
        GoogleAvatarUrl = NormalizeAvatarUrl(avatarUrl);
        if (!HasCustomAvatar)
        {
            AvatarUrl = GoogleAvatarUrl;
        }
        LastLoginAt = now;
    }

    /// <summary>Sets the name the user typed. Refuses an empty, too long or control-character name with <c>invalid_display_name</c>.</summary>
    public void ChangeDisplayName(string? displayName)
    {
        DisplayName = DisplayNamePolicy.Validate(displayName);
        HasCustomDisplayName = true;
    }

    /// <summary>The user's own picture becomes the avatar. The bytes live in <see cref="UserAvatar"/>.</summary>
    public void UseUploadedAvatar()
    {
        AvatarVersion++;
        HasCustomAvatar = true;
        AvatarUrl = UploadedAvatarAddress(Id, AvatarVersion);
    }

    /// <summary>Back to the picture Google sent (none when it sent none).</summary>
    public void UseGoogleAvatar()
    {
        HasCustomAvatar = false;
        AvatarUrl = GoogleAvatarUrl;
    }

    /// <summary>Where the API serves an uploaded avatar. Relative: the SPA adds the API origin.</summary>
    public static string UploadedAvatarAddress(Guid userId, int version) => $"/api/users/{userId}/avatar?v={version}";

    private static string NormalizeDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return FallbackDisplayName;
        }
        return trimmed.Length <= DisplayNameMaxLength ? trimmed : trimmed[..DisplayNameMaxLength];
    }

    /// <summary>
    /// Keeps the avatar only when it is an absolute https URL on a Google-hosted image host
    /// (<see cref="AllowedAvatarHostSuffix"/>). The leading dot stops look-alikes such as
    /// <c>evilgoogleusercontent.com</c>. Anything else is dropped, so the client never loads
    /// an arbitrary third-party image.
    /// </summary>
    private static string? NormalizeAvatarUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl) || avatarUrl.Length > AvatarUrlMaxLength)
        {
            return null;
        }

        if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.EndsWith(AllowedAvatarHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return uri.ToString();
    }
}

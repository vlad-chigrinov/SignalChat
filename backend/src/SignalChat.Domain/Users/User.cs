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
    public const string FallbackDisplayName = "SignalChat user";

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

    public string? AvatarUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastLoginAt { get; private set; }

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
        DisplayName = NormalizeDisplayName(displayName);
        AvatarUrl = NormalizeAvatarUrl(avatarUrl);
        LastLoginAt = now;
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return FallbackDisplayName;
        }
        return trimmed.Length <= DisplayNameMaxLength ? trimmed : trimmed[..DisplayNameMaxLength];
    }

    private static string? NormalizeAvatarUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl) || avatarUrl.Length > AvatarUrlMaxLength)
        {
            return null;
        }
        // Only absolute https URLs are rendered by the client; anything else is dropped.
        return Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.ToString()
            : null;
    }
}

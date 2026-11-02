namespace SignalChat.Domain.Users;

/// <summary>
/// The picture a user uploaded, already re-encoded by the server (see the avatar image processor). Kept apart from
/// <see cref="User"/> so that loading a user never loads the bytes. One row per user at most.
/// </summary>
public sealed class UserAvatar
{
    private UserAvatar()
    {
        // Required by EF Core.
        Image = [];
    }

    public Guid UserId { get; private set; }

    public byte[] Image { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserAvatar Create(Guid userId, byte[] image, DateTimeOffset now) =>
        new() { UserId = userId, Image = image, UpdatedAt = now };

    public void Replace(byte[] image, DateTimeOffset now)
    {
        Image = image;
        UpdatedAt = now;
    }
}

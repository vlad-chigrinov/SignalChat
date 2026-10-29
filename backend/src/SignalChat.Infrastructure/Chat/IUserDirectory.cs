namespace SignalChat.Infrastructure.Chat;

public interface IUserDirectory
{
    /// <summary>Users whose display name contains the query, excluding the caller. Never returns emails.</summary>
    Task<IReadOnlyList<ChatUser>> SearchAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken);

    /// <summary>The public profile of one user, or null when the user does not exist.</summary>
    Task<ChatUser?> FindPublicAsync(Guid userId, CancellationToken cancellationToken);
}

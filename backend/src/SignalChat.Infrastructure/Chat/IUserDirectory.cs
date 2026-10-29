namespace SignalChat.Infrastructure.Chat;

public interface IUserDirectory
{
    /// <summary>Users whose display name contains the query, excluding the caller. Never returns emails.</summary>
    Task<IReadOnlyList<ChatUser>> SearchAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken);
}

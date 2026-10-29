using SignalChat.Domain.Users;

namespace SignalChat.Infrastructure.Users;

public interface IUserAccountService
{
    /// <summary>Creates the user on first sign-in, refreshes the profile afterwards.</summary>
    Task<User> SignInWithGoogleAsync(GoogleProfile profile, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's current session version, or null when the user no longer exists.</summary>
    Task<int?> FindSessionVersionAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Invalidates every session of the user on every device. A missing user is a no-op.</summary>
    Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);
}

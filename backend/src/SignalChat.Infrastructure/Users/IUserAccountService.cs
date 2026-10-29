using SignalChat.Domain.Users;

namespace SignalChat.Infrastructure.Users;

public interface IUserAccountService
{
    /// <summary>Creates the user on first sign-in, refreshes the profile afterwards.</summary>
    Task<User> SignInWithGoogleAsync(GoogleProfile profile, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}

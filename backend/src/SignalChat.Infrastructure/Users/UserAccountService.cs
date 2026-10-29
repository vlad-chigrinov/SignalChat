using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Users;

public sealed class UserAccountService(SignalChatDbContext dbContext, TimeProvider timeProvider) : IUserAccountService
{
    public async Task<User> SignInWithGoogleAsync(GoogleProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            return await UpsertAsync(profile, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two first sign-ins of the same account raced on the unique GoogleSubject index:
            // the other request created the row, so this attempt becomes an update.
            dbContext.ChangeTracker.Clear();
            return await UpsertAsync(profile, cancellationToken);
        }
    }

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    private async Task<User> UpsertAsync(GoogleProfile profile, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(existing => existing.GoogleSubject == profile.Subject, cancellationToken);

        if (user is null)
        {
            user = User.RegisterFromGoogle(profile.Subject, profile.Email, profile.Name, profile.PictureUrl, now);
            dbContext.Users.Add(user);
        }
        else
        {
            user.RecordLogin(profile.Email, profile.Name, profile.PictureUrl, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}

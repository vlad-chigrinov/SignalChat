using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Users;

public sealed class ProfileService(
    SignalChatDbContext dbContext, IAvatarImageProcessor imageProcessor, TimeProvider timeProvider) : IProfileService
{
    public async Task<User> ChangeDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        user.ChangeDisplayName(displayName);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> UploadAvatarAsync(Guid userId, byte[] upload, CancellationToken cancellationToken)
    {
        // Checked before the user is read: a refused file costs no database work.
        var image = imageProcessor.Process(upload);

        var user = await LoadAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var stored = await dbContext.UserAvatars.SingleOrDefaultAsync(avatar => avatar.UserId == userId, cancellationToken);
        if (stored is null)
        {
            dbContext.UserAvatars.Add(UserAvatar.Create(userId, image, now));
        }
        else
        {
            stored.Replace(image, now);
        }

        user.UseUploadedAvatar();
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> ResetAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var stored = await dbContext.UserAvatars.SingleOrDefaultAsync(avatar => avatar.UserId == userId, cancellationToken);
        if (stored is not null)
        {
            dbContext.UserAvatars.Remove(stored);
        }

        user.UseGoogleAvatar();
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<byte[]?> FindAvatarImageAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserAvatars.AsNoTracking()
            .Where(avatar => avatar.UserId == userId)
            .Select(avatar => (byte[]?)avatar.Image)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<User> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users.SingleOrDefaultAsync(existing => existing.Id == userId, cancellationToken)
        ?? throw new ChatRuleException("not_found", "The user does not exist.");
}

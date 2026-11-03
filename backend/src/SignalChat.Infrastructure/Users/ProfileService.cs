using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Media;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Users;

public sealed class ProfileService(
    SignalChatDbContext dbContext, IAvatarImageProcessor imageProcessor, IMediaStorage storage,
    ILogger<ProfileService> logger) : IProfileService
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
        // Checked before the user is read: a refused file costs no database or store work.
        var image = imageProcessor.Process(upload);

        var user = await LoadAsync(userId, cancellationToken);
        try
        {
            await storage.PutAsync(AvatarKey(userId), image, AvatarLimits.StoredContentType, cancellationToken);
        }
        catch (MediaStorageException failure)
        {
            logger.LogWarning(failure, "The object store did not take an avatar.");
            throw new ChatRuleException(AttachmentErrorCodes.StorageUnavailable, "The avatar could not be stored. Try again later.");
        }

        user.UseUploadedAvatar();
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> ResetAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        user.UseGoogleAvatar();
        await dbContext.SaveChangesAsync(cancellationToken);

        // After the save: the user is already back on the Google picture, so a store that fails here leaves only an
        // unreachable object, which the next upload overwrites.
        try
        {
            await storage.DeleteAsync([AvatarKey(userId)], cancellationToken);
        }
        catch (MediaStorageException failure)
        {
            logger.LogError(failure, "The avatar object of a reset profile could not be removed from the object store.");
        }

        return user;
    }

    public async Task<MediaObject?> OpenAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await storage.OpenAsync(AvatarKey(userId), cancellationToken);
        }
        catch (MediaStorageException failure)
        {
            logger.LogWarning(failure, "The object store did not return an avatar.");
            throw new ChatRuleException(AttachmentErrorCodes.StorageUnavailable, "The avatar could not be read. Try again later.");
        }
    }

    /// <summary>One object per user: a new upload replaces the old one, and the version in the address busts the caches.</summary>
    private static string AvatarKey(Guid userId) => $"avatars/{userId:N}.webp";

    private async Task<User> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users.SingleOrDefaultAsync(existing => existing.Id == userId, cancellationToken)
        ?? throw new ChatRuleException("not_found", "The user does not exist.");
}

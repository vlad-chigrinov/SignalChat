using Microsoft.EntityFrameworkCore;
using SignalChat.Infrastructure.Media;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class AttachmentReader(SignalChatDbContext dbContext, IConversationAccess access, IMediaStorage storage) : IAttachmentReader
{
    public async Task<AttachmentFile?> OpenAsync(
        Guid userId, Guid attachmentId, AttachmentVariant variant, CancellationToken cancellationToken)
    {
        var found = await dbContext.MessageAttachments.AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId)
            .Join(
                dbContext.Messages.Where(message => message.DeletedAt == null),
                attachment => attachment.MessageId,
                message => message.Id,
                (attachment, message) => new
                {
                    attachment.OriginalKey,
                    attachment.ThumbnailKey,
                    attachment.ContentType,
                    attachment.FileName,
                    ConversationId = message.RoomId ?? message.DirectConversationId ?? Guid.Empty,
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        // The permission is the conversation's: whoever may read its history may read its pictures.
        if (await access.FindForMemberAsync(userId, found.ConversationId, cancellationToken) is null)
        {
            return null;
        }

        var key = variant == AttachmentVariant.Thumbnail ? found.ThumbnailKey : found.OriginalKey;
        var content = await storage.OpenAsync(key, cancellationToken);
        return content is null ? null : new AttachmentFile(content, found.ContentType, found.FileName);
    }
}

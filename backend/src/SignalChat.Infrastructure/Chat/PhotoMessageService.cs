using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Media;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Infrastructure.Chat;

public sealed class PhotoMessageService(
    SignalChatDbContext dbContext,
    IConversationAccess access,
    IPhotoImageProcessor processor,
    IMediaStorage storage,
    TimeProvider timeProvider,
    ILogger<PhotoMessageService> logger) : IPhotoMessageService
{
    public async Task<PhotoMessageResult> SendAsync(
        Guid userId, Guid conversationId, string? caption, IAsyncEnumerable<PhotoUpload> uploads, CancellationToken cancellationToken)
    {
        // Writing needs an active conversation: a pending request accepts no messages from its initiator.
        var conversation = await access.FindActiveAsync(userId, conversationId, cancellationToken)
            ?? throw await NotActiveAsync(userId, conversationId, cancellationToken);

        // A caption that can never be saved is refused before the first file is read, not after the pictures are stored.
        if ((caption ?? string.Empty).Trim().Length > Message.MaxTextLength)
        {
            throw new ChatRuleException("invalid_message", $"The caption must be at most {Message.MaxTextLength} characters.");
        }

        var writtenKeys = new List<string>();
        try
        {
            var photos = await StorePhotosAsync(uploads, writtenKeys, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var message = conversation.Kind == ConversationKind.Room
                ? Message.InRoom(conversationId, userId, caption, now, photos)
                : Message.InDirectConversation(conversationId, userId, caption, now, photos);

            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync(cancellationToken);

            var author = await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new ChatUser(user.Id, user.DisplayName, user.AvatarUrl))
                .SingleAsync(cancellationToken);
            var view = new MessageView(message.Id, conversationId, author, message.Text, message.CreatedAt)
            {
                Attachments = AttachmentMapping.ToViews(message.Attachments),
            };
            return new PhotoMessageResult(view, conversation);
        }
        catch
        {
            await RemoveWrittenAsync(writtenKeys);
            throw;
        }
    }

    private async Task<List<PhotoDraft>> StorePhotosAsync(
        IAsyncEnumerable<PhotoUpload> uploads, List<string> writtenKeys, CancellationToken cancellationToken)
    {
        var drafts = new List<PhotoDraft>();
        var totalBytes = 0L;
        await foreach (var upload in uploads.WithCancellation(cancellationToken))
        {
            if (drafts.Count == Message.MaxPhotos)
            {
                throw new ChatRuleException(AttachmentErrorCodes.InvalidAttachments, $"A message carries at most {Message.MaxPhotos} photos.");
            }

            totalBytes += upload.Content.Length;
            if (upload.Content.Length > PhotoLimits.MaxFileBytes || totalBytes > PhotoLimits.MaxTotalBytes)
            {
                throw new ChatRuleException(AttachmentErrorCodes.AttachmentTooLarge, "The photos are too large.");
            }

            var processed = await processor.ProcessAsync(upload.Content, cancellationToken);
            drafts.Add(await StoreAsync(upload.FileName, processed, writtenKeys, cancellationToken));
        }

        return drafts.Count > 0
            ? drafts
            : throw new ChatRuleException(AttachmentErrorCodes.InvalidAttachments, "A photo message carries at least one photo.");
    }

    private async Task<PhotoDraft> StoreAsync(
        string? uploadedName, ProcessedPhoto processed, List<string> writtenKeys, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7(timeProvider.GetUtcNow());
        var originalKey = $"photos/{id:N}.webp";
        var thumbnailKey = $"photos/{id:N}.thumb.webp";
        try
        {
            // A key is noted before its write: an object a failing store half-took is removed with the rest.
            writtenKeys.Add(originalKey);
            await storage.PutAsync(originalKey, processed.Original, PhotoLimits.StoredContentType, cancellationToken);
            writtenKeys.Add(thumbnailKey);
            await storage.PutAsync(thumbnailKey, processed.Thumbnail, PhotoLimits.StoredContentType, cancellationToken);
        }
        catch (MediaStorageException failure)
        {
            logger.LogWarning(failure, "The object store did not take a chat photo.");
            throw new ChatRuleException(AttachmentErrorCodes.StorageUnavailable, "The photos could not be stored. Try again later.");
        }

        return new PhotoDraft(
            id, originalKey, thumbnailKey, processed.Width, processed.Height, processed.ThumbnailWidth, processed.ThumbnailHeight,
            processed.Original.LongLength, PhotoLimits.StoredContentType, PhotoFileName.Normalize(uploadedName));
    }

    // Best effort and not tied to the request: a client that went away must not leave objects behind either.
    private async Task RemoveWrittenAsync(List<string> writtenKeys)
    {
        if (writtenKeys.Count == 0)
        {
            return;
        }

        try
        {
            await storage.DeleteAsync(writtenKeys, CancellationToken.None);
        }
        catch (MediaStorageException failure)
        {
            logger.LogError(failure, "{Count} objects of a refused photo message could not be removed from the object store.", writtenKeys.Count);
        }
    }

    /// <summary>
    /// The initiator of a pending request can see it but not write to it: "dm_not_accepted". Everyone else who
    /// is not active gets "not_found", the same answer as for a missing conversation.
    /// </summary>
    private async Task<ChatRuleException> NotActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await access.FindForMemberAsync(userId, conversationId, cancellationToken) is not null
            ? new ChatRuleException(DirectRequestErrorCodes.NotAccepted, "The request has not been accepted yet.")
            : new ChatRuleException("not_found", "Conversation not found.");
}

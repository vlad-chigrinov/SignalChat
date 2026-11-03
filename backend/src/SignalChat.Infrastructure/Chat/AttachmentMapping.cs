using SignalChat.Domain.Chat;

namespace SignalChat.Infrastructure.Chat;

/// <summary>Turns stored attachments into the views the API sends; the object keys stay on the server.</summary>
public static class AttachmentMapping
{
    public static IReadOnlyList<AttachmentView> ToViews(IEnumerable<MessageAttachment> attachments) =>
        attachments
            .OrderBy(attachment => attachment.Position)
            .Select(attachment => new AttachmentView(
                attachment.Id, attachment.Width, attachment.Height, attachment.ThumbnailWidth, attachment.ThumbnailHeight,
                attachment.SizeBytes, attachment.ContentType, attachment.FileName))
            .ToList();
}

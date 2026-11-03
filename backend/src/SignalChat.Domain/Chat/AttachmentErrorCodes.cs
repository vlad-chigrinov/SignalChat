namespace SignalChat.Domain.Chat;

/// <summary>Stable codes of the photo rules. They travel as <c>ChatRuleException.Code</c>, so the REST answer carries them.</summary>
public static class AttachmentErrorCodes
{
    /// <summary>The request carries no photo, more than the limit, or is not a well-formed upload.</summary>
    public const string InvalidAttachments = "invalid_attachments";

    /// <summary>A file is not a usable picture: wrong content, damaged, or beyond the pixel limits.</summary>
    public const string InvalidImage = "invalid_image";

    /// <summary>One file, or all files together, are larger than the limit.</summary>
    public const string AttachmentTooLarge = "attachment_too_large";

    /// <summary>The object store did not take or give the file. Nothing of the message was kept.</summary>
    public const string StorageUnavailable = "storage_unavailable";
}

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Media;

namespace SignalChat.Api.Chat.Photos;

/// <summary>
/// Reads the multipart body of a photo message section by section, straight off the request stream: no temporary files,
/// and only one photo in memory at a time. The caption field (<c>text</c>, optional) must come before the photos; every
/// part with a file name is a photo, whatever its field name or media type says.
/// </summary>
internal sealed class PhotoUploadForm
{
    public const string TextField = "text";

    private const int MaxBoundaryLength = 128;
    private const int CopyBufferBytes = 81920;
    private const int Utf8BytesPerCharacter = 4;

    private readonly MultipartReader _reader;
    private MultipartSection? _firstPhoto;

    private PhotoUploadForm(MultipartReader reader) => _reader = reader;

    public static PhotoUploadForm Open(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Send the photos as multipart/form-data.");
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > MaxBoundaryLength)
        {
            throw Invalid("The multipart boundary is missing or too long.");
        }
        return new PhotoUploadForm(new MultipartReader(boundary, request.Body));
    }

    /// <summary>The caption, or null when the request has none. Stops at the first photo, which stays unread.</summary>
    public async Task<string?> ReadCaptionAsync(CancellationToken cancellationToken)
    {
        var section = await _reader.ReadNextSectionAsync(cancellationToken);
        if (section is null)
        {
            return null;
        }
        if (IsPhoto(section, out _))
        {
            _firstPhoto = section;
            return null;
        }
        if (!NameOf(section).Equals(TextField, StringComparison.Ordinal))
        {
            throw Invalid("Only the fields text and files are accepted.");
        }
        return await ReadFieldAsync(section, cancellationToken);
    }

    /// <summary>The photos one after another; the next one is read only when the previous one has been taken.</summary>
    public async IAsyncEnumerable<PhotoUpload> ReadPhotosAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var section = _firstPhoto ?? await _reader.ReadNextSectionAsync(cancellationToken);
        _firstPhoto = null;
        while (section is not null)
        {
            if (!IsPhoto(section, out var fileName))
            {
                throw Invalid("The caption must come before the photos, and only the fields text and files are accepted.");
            }
            yield return new PhotoUpload(fileName, await ReadFileAsync(section, cancellationToken));
            section = await _reader.ReadNextSectionAsync(cancellationToken);
        }
    }

    private static async Task<string> ReadFieldAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedAsync(section.Body, Message.MaxTextLength * Utf8BytesPerCharacter, cancellationToken);
        return bytes.Length > Message.MaxTextLength * Utf8BytesPerCharacter
            ? throw new ChatRuleException("invalid_message", $"The caption must be at most {Message.MaxTextLength} characters.")
            : Encoding.UTF8.GetString(bytes.Span);
    }

    private static async Task<ReadOnlyMemory<byte>> ReadFileAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedAsync(section.Body, PhotoLimits.MaxFileBytes, cancellationToken);
        return bytes.Length > PhotoLimits.MaxFileBytes
            ? throw new ChatRuleException(AttachmentErrorCodes.AttachmentTooLarge, "A photo is too large.")
            : bytes;
    }

    // Reads at most limit + 1 bytes: one past the limit is enough to say "too large", and the rest of the part is never read.
    private static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(Stream body, int limit, CancellationToken cancellationToken)
    {
        var collected = new MemoryStream();
        var buffer = new byte[CopyBufferBytes];
        while (collected.Length <= limit)
        {
            var wanted = (int)Math.Min(buffer.Length, limit + 1 - collected.Length);
            var read = await body.ReadAsync(buffer.AsMemory(0, wanted), cancellationToken);
            if (read == 0)
            {
                break;
            }
            collected.Write(buffer, 0, read);
        }
        return collected.TryGetBuffer(out var segment) ? segment.AsMemory() : ReadOnlyMemory<byte>.Empty;
    }

    private static bool IsPhoto(MultipartSection section, out string? fileName)
    {
        fileName = null;
        if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || !disposition.IsFileDisposition())
        {
            return false;
        }
        fileName = HeaderUtilities.RemoveQuotes(disposition.FileName.HasValue ? disposition.FileName : disposition.FileNameStar).Value;
        return true;
    }

    private static string NameOf(MultipartSection section) =>
        ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
            ? HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty
            : string.Empty;

    private static ChatRuleException Invalid(string message) => new(AttachmentErrorCodes.InvalidAttachments, message);
}

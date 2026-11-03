using System.Runtime.InteropServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace SignalChat.Infrastructure.Media;

/// <summary>
/// The object store over the S3 protocol, as RustFS speaks it. The bucket is addressed in the path (a container name has
/// no wildcard DNS), the request checksums are sent only where the protocol needs them (older S3-compatible servers do
/// not know the newer trailing checksums), and nothing but the bucket of this environment is touched.
/// </summary>
public sealed class S3MediaStorage : IMediaStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3MediaStorage(MediaStorageSettings settings)
    {
        _bucket = settings.Bucket;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = settings.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest { BucketName = _bucket, Key = key, ContentType = contentType };
        await Guard(async () =>
        {
            using var body = MemoryMarshal.TryGetArray(content, out var segment)
                ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
                : new MemoryStream(content.ToArray(), writable: false);
            request.InputStream = body;
            await _client.PutObjectAsync(request, cancellationToken);
        });
    }

    public async Task<MediaObject?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
            return new MediaObject(response.ResponseStream, response.ContentLength, response);
        }
        catch (AmazonS3Exception notFound) when (notFound.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception failure) when (failure is AmazonServiceException or HttpRequestException or IOException)
        {
            throw new MediaStorageException("The object store could not be read.", failure);
        }
    }

    public async Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            await Guard(() => _client.DeleteObjectAsync(_bucket, key, cancellationToken));
        }
    }

    public void Dispose() => _client.Dispose();

    private static async Task Guard(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception failure) when (failure is AmazonServiceException or HttpRequestException or IOException)
        {
            throw new MediaStorageException("The object store refused or could not take the request.", failure);
        }
    }
}

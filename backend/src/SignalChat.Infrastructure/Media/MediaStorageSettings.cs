using Microsoft.Extensions.Configuration;

namespace SignalChat.Infrastructure.Media;

/// <summary>
/// Where the S3-compatible object store (RustFS) is and how to sign in to it: <c>MEDIA_S3_*</c> in the environment.
/// Each environment has its own bucket and its own access key, which can touch that bucket only.
/// </summary>
public sealed record MediaStorageSettings(string Endpoint, string Bucket, string AccessKey, string SecretKey, string Region)
{
    public const string EndpointKey = "MEDIA_S3_ENDPOINT";
    public const string BucketKey = "MEDIA_S3_BUCKET";
    public const string AccessKeyKey = "MEDIA_S3_ACCESS_KEY";
    public const string SecretKeyKey = "MEDIA_S3_SECRET_KEY";
    public const string RegionKey = "MEDIA_S3_REGION";

    /// <summary>RustFS has no regions; the signature still names one, and both sides must agree.</summary>
    public const string DefaultRegion = "us-east-1";

    /// <summary>The settings, or null when no endpoint is configured. A half-configured store is an error, not a silent fallback.</summary>
    public static MediaStorageSettings? FromConfiguration(IConfiguration configuration)
    {
        var endpoint = configuration[EndpointKey];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return new MediaStorageSettings(
            endpoint.Trim(),
            Required(configuration, BucketKey),
            Required(configuration, AccessKeyKey),
            Required(configuration, SecretKeyKey),
            configuration[RegionKey] is { Length: > 0 } region ? region.Trim() : DefaultRegion);
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{key} is missing: {EndpointKey} is set, so the object store needs all of its settings.");
}

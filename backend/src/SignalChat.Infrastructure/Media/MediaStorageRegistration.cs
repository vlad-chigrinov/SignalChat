using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SignalChat.Infrastructure.Media;

public static class MediaStorageRegistration
{
    /// <summary>
    /// Registers the picture processor and the object store. With <c>MEDIA_S3_*</c> set the store is RustFS (S3). Without it a
    /// production host refuses to start, since pictures kept in memory would vanish with the process; any other host
    /// keeps them in memory, so local development needs no extra container.
    /// </summary>
    public static IServiceCollection AddMediaStorage(
        this IServiceCollection services, IConfiguration configuration, bool requireObjectStore)
    {
        services.AddSingleton<IPhotoImageProcessor, PhotoImageProcessor>();

        var settings = MediaStorageSettings.FromConfiguration(configuration);
        if (settings is not null)
        {
            services.AddSingleton<IMediaStorage>(new S3MediaStorage(settings));
        }
        else if (requireObjectStore)
        {
            throw new InvalidOperationException(
                $"{MediaStorageSettings.EndpointKey} is missing: production keeps chat photos in the S3 object store (RustFS).");
        }
        else
        {
            services.AddSingleton<IMediaStorage, InMemoryMediaStorage>();
        }
        return services;
    }
}

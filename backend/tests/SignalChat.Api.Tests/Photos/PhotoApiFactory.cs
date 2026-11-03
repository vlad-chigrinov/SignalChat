using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Api.Tests.Profile;
using SignalChat.Infrastructure.Media;

namespace SignalChat.Api.Tests.Photos;

/// <summary>
/// The API with an in-memory object store that can be told to fail, and a picture processor that can be replaced by a
/// stub, so a test reaches the limits and the rollback without megabytes of real pictures.
/// </summary>
public class PhotoApiFactory : SignalChatApiFactory
{
    public InMemoryMediaStorage Store { get; } = new();

    public FaultyMediaStorage Storage { get; }

    public ScriptedPhotoProcessor Processor { get; } = new(new PhotoImageProcessor());

    public PhotoApiFactory() => Storage = new FaultyMediaStorage(Store);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMediaStorage>(Storage);
            services.AddSingleton<IPhotoImageProcessor>(Processor);
        });
    }
}

/// <summary>Passes calls to a real store until told to fail the n-th <c>Put</c> or every <c>Open</c>.</summary>
public sealed class FaultyMediaStorage(IMediaStorage inner) : IMediaStorage
{
    private int _puts;

    /// <summary>The 1-based number of the Put that throws; zero means none.</summary>
    public int FailPutNumber { get; set; }

    public bool FailOpen { get; set; }

    public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        if (++_puts == FailPutNumber)
        {
            throw new MediaStorageException("The store refused the object.");
        }
        return inner.PutAsync(key, content, contentType, cancellationToken);
    }

    public Task<MediaObject?> OpenAsync(string key, CancellationToken cancellationToken) =>
        FailOpen ? throw new MediaStorageException("The store is down.") : inner.OpenAsync(key, cancellationToken);

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
        inner.DeleteAsync(keys, cancellationToken);
}

/// <summary>The real processor, unless a test gives <see cref="Stub"/>: then every upload becomes that tiny picture, unread.</summary>
public sealed class ScriptedPhotoProcessor(IPhotoImageProcessor real) : IPhotoImageProcessor
{
    private static readonly ProcessedPhoto Tiny = new(TestImages.Webp(8, 8), 8, 8, TestImages.Webp(8, 8), 8, 8);

    public bool Stub { get; set; }

    public Task<ProcessedPhoto> ProcessAsync(ReadOnlyMemory<byte> upload, CancellationToken cancellationToken) =>
        Stub ? Task.FromResult(Tiny) : real.ProcessAsync(upload, cancellationToken);
}

using System.Net;
using SignalChat.Api.Configuration;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Media;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>Production starts only with a persistent key ring; without one the host refuses to start.</summary>
public sealed class ProductionStartupTests
{
    [Fact]
    public void Production_WithoutKeysPath_StopsTheApplicationAtStartup()
    {
        using var factory = new ProductionWithoutKeysApiFactory();

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(DataProtectionSettings.KeysPathKey, error.ToString());
    }

    [Fact]
    public void Production_WithoutAnObjectStore_StopsTheApplicationAtStartup()
    {
        using var factory = new ProductionWithoutObjectStoreApiFactory();

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(MediaStorageSettings.EndpointKey, error.ToString());
    }

    [Fact]
    public async Task Production_WithKeysPath_StartsAndAnswers()
    {
        using var factory = new ProductionWithKeysApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>Only the configured host names reach the application: AllowedHosts comes from the environment, with no wildcard.</summary>
public sealed class AllowedHostsTests
{
    [Fact]
    public async Task ConfiguredPublicHost_IsAccepted()
    {
        using var factory = new PublicHostApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = PublicHostApiFactory.PublicDomain;

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlistedHost_IsRefused()
    {
        using var factory = new PublicHostApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = "evil.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests;

public sealed class HealthEndpointTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOk_WithoutTouchingTheDatabase()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

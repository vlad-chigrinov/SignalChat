using System.Net;
using System.Net.Http.Json;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>Without the flag the route does not exist at all: a regular 404, nothing to discover.</summary>
public sealed class TestLoginDisabledTests
{
    [Fact]
    public async Task WithoutTheFlag_TestLoginIsNotFound()
    {
        using var factory = new SignalChatApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/auth/test-login", new { handle = "alice" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

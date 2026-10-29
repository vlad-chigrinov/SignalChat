using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

public sealed class LoginNotConfiguredTests(GoogleNotConfiguredApiFactory factory) : IClassFixture<GoogleNotConfiguredApiFactory>
{
    [Fact]
    public async Task Login_WithoutGoogleCredentials_Returns503_AndTheApiStillRuns()
    {
        using var client = factory.CreateApiClient();

        var login = await client.GetAsync("/auth/login");
        var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Contains("GOOGLE_CLIENT_ID", await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}

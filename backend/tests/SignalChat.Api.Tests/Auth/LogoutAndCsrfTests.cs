using System.Net;
using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

public sealed class LogoutAndCsrfTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Logout_FromTheSpa_ExpiresTheSessionCookie()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Logout(cookie, SignalChatApiFactory.AllowedOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cleared = SetCookies.Find(response.Headers.GetValues("Set-Cookie"), AuthSchemes.SessionCookieName);
        Assert.NotNull(cleared);
        Assert.True(cleared.Expires < DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Logout_FromForeignOrigin_IsRejectedAsCsrf()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Logout(cookie, SignalChatApiFactory.ForeignOrigin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Logout_WithNullOrigin_IsRejected()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Logout(cookie, "null"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage Logout(string cookie, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", origin);
        return request;
    }
}

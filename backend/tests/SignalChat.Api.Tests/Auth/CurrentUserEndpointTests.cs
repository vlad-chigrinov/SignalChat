using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Auth;

public sealed class CurrentUserEndpointTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Me_WithoutCookie_Returns401_NotARedirect()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Me_WithSession_ReturnsProfile_AndIsNotCached()
    {
        var profile = new GoogleProfile("me-sub", "me@example.com", "Me Myself", "https://lh3.googleusercontent.com/a/me");
        var (user, cookie) = await factory.CreateSessionAsync(profile);
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(WithCookie("/auth/me", cookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(new CurrentUserResponse(user.Id, "Me Myself", "https://lh3.googleusercontent.com/a/me", "me@example.com"), body);
    }

    [Fact]
    public async Task Me_WithTamperedCookie_Returns401()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(WithCookie("/auth/me", $"{AuthSchemes.SessionCookieName}=forged-value"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ForDeletedUser_Returns401_AndClearsCookie()
    {
        var (user, cookie) = await factory.CreateSessionAsync(new GoogleProfile("gone-sub", "gone@example.com", "Gone", null));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
            await db.Users.Where(existing => existing.Id == user.Id).ExecuteDeleteAsync();
        }
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(WithCookie("/auth/me", cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var cleared = SetCookies.Find(response.Headers.GetValues("Set-Cookie"), AuthSchemes.SessionCookieName);
        Assert.NotNull(cleared);
        Assert.True(cleared.Expires < DateTimeOffset.UtcNow);
    }

    private static HttpRequestMessage WithCookie(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return request;
    }
}

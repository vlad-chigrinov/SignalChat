using System.Net;
using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Auth;

public sealed class SessionRevocationTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Logout_MakesTheOldCookieUnusable()
    {
        var (_, cookie) = await factory.CreateSessionAsync(new GoogleProfile("revoke-sub", "revoke@example.com", "Revoke", null));
        using var client = factory.CreateApiClient();

        var logout = await client.SendAsync(Request(HttpMethod.Post, "/auth/logout", cookie, SignalChatApiFactory.AllowedOrigin));
        var me = await client.SendAsync(Request(HttpMethod.Get, "/auth/me", cookie));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheSessionsOfTheSameUserOnOtherDevices()
    {
        var profile = new GoogleProfile("two-devices-sub", "devices@example.com", "Two Devices", null);
        var (_, phoneCookie) = await factory.CreateSessionAsync(profile);
        var (_, laptopCookie) = await factory.CreateSessionAsync(profile);
        using var client = factory.CreateApiClient();

        await client.SendAsync(Request(HttpMethod.Post, "/auth/logout", phoneCookie, SignalChatApiFactory.AllowedOrigin));
        var laptop = await client.SendAsync(Request(HttpMethod.Get, "/auth/me", laptopCookie));

        Assert.Equal(HttpStatusCode.Unauthorized, laptop.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_StillReturnsNoContent()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Request(HttpMethod.Post, "/auth/logout", cookie: null, SignalChatApiFactory.AllowedOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ActiveSession_IsRenewedUntilTheAbsoluteLifetime_ThenEnds()
    {
        // The sliding window (14 days) is renewed by activity; only the 30-day absolute cap ends the session.
        using var clockFactory = new SignalChatApiFactory();
        var (_, cookie) = await clockFactory.CreateSessionAsync(new GoogleProfile("absolute-sub", "absolute@example.com", "Absolute", null));
        using var client = clockFactory.CreateApiClient();
        // 10 + 10 days: each step is inside the 14-day window, so activity renews the cookie.
        foreach (var step in new[] { TimeSpan.FromDays(10), TimeSpan.FromDays(10) })
        {
            clockFactory.Clock.Advance(step);
            var renewed = await client.SendAsync(Request(HttpMethod.Get, "/auth/me", cookie));
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
            cookie = RenewedCookie(renewed, cookie);
        }

        // 29 days 12 hours from sign-in: still inside the absolute lifetime.
        clockFactory.Clock.Advance(TimeSpan.FromDays(9) + TimeSpan.FromHours(12));
        var beforeLimit = await client.SendAsync(Request(HttpMethod.Get, "/auth/me", cookie));
        Assert.Equal(HttpStatusCode.OK, beforeLimit.StatusCode);
        cookie = RenewedCookie(beforeLimit, cookie);

        // 30 days and one minute from sign-in: past the cap, even though the cookie was renewed.
        clockFactory.Clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));
        var afterLimit = await client.SendAsync(Request(HttpMethod.Get, "/auth/me", cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, afterLimit.StatusCode);
    }

    /// <summary>The sliding window re-issues the cookie on activity; keep using the newest one.</summary>
    private static string RenewedCookie(HttpResponseMessage response, string previous)
    {
        var renewed = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? SetCookies.Find(values, AuthSchemes.SessionCookieName)
            : null;
        return renewed?.NameValue() ?? previous;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? cookie, string? origin = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }
        return request;
    }
}

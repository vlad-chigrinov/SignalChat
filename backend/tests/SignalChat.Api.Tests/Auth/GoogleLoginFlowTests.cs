using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>Full OIDC Authorization Code + PKCE round trip against <see cref="FakeGoogle"/>.</summary>
public sealed class GoogleLoginFlowTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Login_RedirectsToGoogle_WithCodeFlowAndPkce_AndBackendRedirectUri()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/auth/login?returnUrl=/rooms");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(FakeGoogle.AuthorizationEndpoint, location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Equal("https://localhost/auth/callback/google", query["redirect_uri"]);
        Assert.Equal(FakeGoogle.ClientId, query["client_id"]);
        Assert.Equal("openid profile email", query["scope"]);
        Assert.DoesNotContain(FakeGoogle.ClientSecret, location.ToString());
    }

    [Fact]
    public async Task Callback_CreatesUser_SetsHardenedSessionCookie_AndLandsOnTheSpa()
    {
        using var client = factory.CreateApiClient();
        factory.Google.NextUser = new FakeGoogleUser("flow-sub", "flow@example.com", "Flow User", EmailVerified: true);

        // 1. /auth/login → Google authorize URL (+ correlation and nonce cookies).
        var login = await client.GetAsync("/auth/login?returnUrl=/rooms/42");
        var authorize = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        factory.Google.ExpectedNonce = authorize["nonce"];

        // 2. Google redirects the browser back with a code; the backend redeems it server-side.
        using var callback = new HttpRequestMessage(
            HttpMethod.Get, $"/auth/callback/google?code=fake-code&state={Uri.EscapeDataString(authorize["state"]!)}");
        callback.Headers.Add("Cookie", SetCookies.ToCookieHeader(login));
        var callbackResponse = await client.SendAsync(callback);

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal("http://localhost:5173/rooms/42", callbackResponse.Headers.Location!.ToString());

        // PKCE: the token request carried the verifier, and the server authenticated with its secret.
        var tokenRequest = factory.Google.LastTokenRequest;
        Assert.Equal("authorization_code", tokenRequest["grant_type"]);
        Assert.Equal("fake-code", tokenRequest["code"]);
        Assert.False(string.IsNullOrEmpty(tokenRequest["code_verifier"]));

        var session = SetCookies.Find(callbackResponse.Headers.GetValues("Set-Cookie"), AuthSchemes.SessionCookieName);
        Assert.NotNull(session);
        AssertHardened(session);

        // 3. The cookie is the session: /auth/me knows the new user.
        using var me = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        me.Headers.Add("Cookie", session.NameValue());
        var meResponse = await client.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var body = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal("flow@example.com", body!.Email);
        Assert.Equal("Flow User", body.DisplayName);

        // No Google token ever reaches the browser.
        var everything = string.Join('\n', callbackResponse.Headers.GetValues("Set-Cookie")) + await meResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fake-google-access-token", everything);
        Assert.DoesNotContain("id_token", everything);
    }

    [Fact]
    public async Task Callback_WithUnverifiedEmail_DoesNotSignIn()
    {
        using var client = factory.CreateApiClient();
        factory.Google.NextUser = new FakeGoogleUser("unverified-sub", "nope@example.com", "Nope", EmailVerified: false);

        var login = await client.GetAsync("/auth/login");
        var authorize = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        factory.Google.ExpectedNonce = authorize["nonce"];
        using var callback = new HttpRequestMessage(
            HttpMethod.Get, $"/auth/callback/google?code=fake-code&state={Uri.EscapeDataString(authorize["state"]!)}");
        callback.Headers.Add("Cookie", SetCookies.ToCookieHeader(login));

        var response = await client.SendAsync(callback);

        Assert.Equal("http://localhost:5173/?authError=google", response.Headers.Location!.ToString());
        Assert.Null(SetCookies.Find(SetCookieValues(response), AuthSchemes.SessionCookieName));
    }

    [Fact]
    public async Task Callback_WithForgedState_DoesNotSignIn()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/auth/callback/google?code=fake-code&state=forged");

        Assert.Equal("http://localhost:5173/?authError=google", response.Headers.Location!.ToString());
        Assert.Null(SetCookies.Find(SetCookieValues(response), AuthSchemes.SessionCookieName));
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Login_IgnoresNonLocalReturnUrl(string returnUrl)
    {
        using var client = factory.CreateApiClient();
        factory.Google.NextUser = new FakeGoogleUser("redirect-sub", "redirect@example.com", "Redirect", EmailVerified: true);

        var login = await client.GetAsync($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        var authorize = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        factory.Google.ExpectedNonce = authorize["nonce"];
        using var callback = new HttpRequestMessage(
            HttpMethod.Get, $"/auth/callback/google?code=fake-code&state={Uri.EscapeDataString(authorize["state"]!)}");
        callback.Headers.Add("Cookie", SetCookies.ToCookieHeader(login));

        var response = await client.SendAsync(callback);

        Assert.Equal("http://localhost:5173/", response.Headers.Location!.ToString());
    }

    private static void AssertHardened(SetCookieHeaderValue cookie)
    {
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.True(string.IsNullOrEmpty(cookie.Domain.ToString()));
    }

    private static IEnumerable<string> SetCookieValues(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
}

using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>Behind Traefik the browser-facing scheme and host arrive in X-Forwarded-*, and only a trusted proxy may set them.</summary>
public sealed class ForwardedHeadersTests
{
    private const string PublicHost = "chat.vlados.tech";
    private const string PublicCallbackUri = "https://chat.vlados.tech/auth/callback/google";
    private const string LocalCallbackUri = "https://localhost/auth/callback/google";
    private const string RedirectUriParameter = "redirect_uri";

    [Fact]
    public async Task ForwardedHeaders_FromTrustedProxy_BuildTheGoogleRedirectUriOnThePublicHost()
    {
        using var factory = new TrustedLoopbackProxyApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await SendLoginAsync(client, forwardedProto: "https", forwardedHost: PublicHost);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PublicCallbackUri, RedirectUriOf(response));
    }

    [Fact]
    public async Task ForwardedHeaders_FromUntrustedAddress_AreIgnored()
    {
        // The default trusted list is the private Docker ranges; a public client address is not one of them.
        using var factory = new PublicClientApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await SendLoginAsync(client, forwardedProto: "https", forwardedHost: PublicHost);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(LocalCallbackUri, RedirectUriOf(response));
    }

    [Fact]
    public async Task ForwardedProtoHttps_FromTrustedProxy_DoesNotLoopOnTheHttpsRedirect()
    {
        using var factory = new TrustedLoopbackProxyApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", PublicHost);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string forwardedProto, string forwardedHost)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/login?returnUrl=/");
        request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        request.Headers.Add("X-Forwarded-Host", forwardedHost);
        return client.SendAsync(request);
    }

    private static string? RedirectUriOf(HttpResponseMessage response) =>
        QueryHelpers.ParseQuery(response.Headers.Location!.Query)[RedirectUriParameter].ToString();
}

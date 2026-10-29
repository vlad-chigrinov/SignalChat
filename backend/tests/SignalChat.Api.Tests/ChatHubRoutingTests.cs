using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests;

/// <summary>The session cookie is what authenticates a SignalR connection.</summary>
public sealed class ChatHubRoutingTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    private const string NegotiatePath = "/hubs/chat/negotiate?negotiateVersion=1";

    [Fact]
    public async Task Negotiate_WithoutSession_IsUnauthorized()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Negotiate(cookie: null, SignalChatApiFactory.AllowedOrigin));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithSessionCookie_Succeeds()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Negotiate(cookie, SignalChatApiFactory.AllowedOrigin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("connectionToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Negotiate_FromForeignOrigin_IsForbidden_EvenWithSession()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Negotiate(cookie, SignalChatApiFactory.ForeignOrigin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WebSocketHandshake_FromForeignOrigin_IsForbidden()
    {
        var (_, cookie) = await factory.CreateSessionAsync();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/hubs/chat?id=any");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", SignalChatApiFactory.ForeignOrigin);
        request.Headers.Add("Upgrade", "websocket");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage Negotiate(string? cookie, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, NegotiatePath);
        request.Headers.Add("Origin", origin);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        return request;
    }
}

using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Security;

public sealed class CorsTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task Preflight_FromAllowedOrigin_AllowsCredentials()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Preflight(SignalChatApiFactory.AllowedOrigin));

        Assert.Equal(SignalChatApiFactory.AllowedOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_FromForeignOrigin_GetsNoCorsGrant()
    {
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Preflight(SignalChatApiFactory.ForeignOrigin));

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
        Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task SimpleRequest_FromForeignOrigin_GetsNoCorsGrant()
    {
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Origin", SignalChatApiFactory.ForeignOrigin);

        var response = await client.SendAsync(request);

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/auth/logout");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}

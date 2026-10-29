using System.Net;
using SignalChat.Api.Security;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Security;

public sealed class HostAndHeadersTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task RequestForAnUnlistedHost_IsRejectedWith400()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("https://evil.example/health");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Responses_CarryTheHardeningHeaders()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(SecurityHeadersMiddleware.NoSniff, response.Headers.GetValues(SecurityHeadersMiddleware.ContentTypeOptionsHeader).Single());
        Assert.Equal(SecurityHeadersMiddleware.NoReferrer, response.Headers.GetValues(SecurityHeadersMiddleware.ReferrerPolicyHeader).Single());
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, response.Headers.GetValues(SecurityHeadersMiddleware.ContentSecurityPolicyHeader).Single());
    }
}

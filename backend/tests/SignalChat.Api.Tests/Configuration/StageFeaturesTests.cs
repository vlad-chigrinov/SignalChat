using System.Net;
using Microsoft.Extensions.Configuration;
using SignalChat.Api.Configuration;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>Swagger and the demo data exist for the stage environment (ASP.NET Core Development) and nowhere else.</summary>
public sealed class StageFeaturesTests
{
    [Fact]
    public async Task Swagger_InDevelopment_ServesTheApiDescription()
    {
        using var factory = new DevelopmentApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/rooms", body);
        Assert.Contains("/auth/me", body);
    }

    [Fact]
    public async Task Swagger_InDevelopment_DescribesTheSignalRHubMethods()
    {
        using var factory = new DevelopmentApiFactory();
        using var client = factory.CreateApiClient();

        var body = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/hubs/chat/JoinRoom", body);
        Assert.Contains("/hubs/chat/SendMessage", body);
    }

    [Fact]
    public async Task SwaggerUi_InDevelopment_IsNotBlockedByTheApiCsp()
    {
        using var factory = new DevelopmentApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Api_InDevelopment_KeepsTheStrictCspOutsideSwagger()
    {
        using var factory = new DevelopmentApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Swagger_OutsideDevelopment_IsNotThere()
    {
        using var factory = new SignalChatApiFactory();
        using var client = factory.CreateApiClient();

        var description = await client.GetAsync("/swagger/v1/swagger.json");
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, description.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    public void DemoData_IsAllowedInDevelopment(string environment)
    {
        var settings = DemoDataSettings.FromConfiguration(Configure(enabled: true));

        settings.EnsureAllowedIn(environment);

        Assert.True(settings.Enabled);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    [InlineData("E2E")]
    public void DemoData_IsRefusedOutsideDevelopment_AndNamesTheFlag(string environment)
    {
        var settings = DemoDataSettings.FromConfiguration(Configure(enabled: true));

        var error = Assert.Throws<InvalidOperationException>(() => settings.EnsureAllowedIn(environment));

        Assert.Contains(DemoDataSettings.FlagKey, error.Message);
    }

    [Fact]
    public void DemoData_IsOffByDefault_InEveryEnvironment()
    {
        var settings = DemoDataSettings.FromConfiguration(Configure(enabled: null));

        settings.EnsureAllowedIn("Production");

        Assert.False(settings.Enabled);
    }

    private static IConfiguration Configure(bool? enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(enabled is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { [DemoDataSettings.FlagKey] = enabled.Value ? "true" : "false" })
            .Build();
}

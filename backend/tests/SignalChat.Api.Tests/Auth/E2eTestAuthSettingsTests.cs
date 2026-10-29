using Microsoft.Extensions.Configuration;
using SignalChat.Api.Auth;

namespace SignalChat.Api.Tests.Auth;

/// <summary>The rule behind the flag: off by default, and on only in the test environments.</summary>
public sealed class E2eTestAuthSettingsTests
{
    [Fact]
    public void Flag_IsOffByDefault()
    {
        var settings = E2eTestAuthSettings.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.False(settings.Enabled);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("E2E")]
    public void Flag_IsAllowed_InTestEnvironments(string environmentName)
    {
        var settings = EnabledSettings();

        settings.EnsureAllowedIn(environmentName);
        Assert.True(settings.Enabled);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Flag_IsRefused_OutsideTestEnvironments(string environmentName)
    {
        var error = Assert.Throws<InvalidOperationException>(() => EnabledSettings().EnsureAllowedIn(environmentName));

        Assert.Contains(E2eTestAuthSettings.FlagKey, error.Message);
        Assert.Contains(environmentName, error.Message);
    }

    [Fact]
    public void Flag_OffInProduction_IsAllowed()
    {
        var settings = E2eTestAuthSettings.FromConfiguration(new ConfigurationBuilder().Build());

        settings.EnsureAllowedIn("Production");
        Assert.False(settings.Enabled);
    }

    private static E2eTestAuthSettings EnabledSettings() =>
        E2eTestAuthSettings.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(E2eTestAuthSettings.FlagKey, "true")])
            .Build());
}

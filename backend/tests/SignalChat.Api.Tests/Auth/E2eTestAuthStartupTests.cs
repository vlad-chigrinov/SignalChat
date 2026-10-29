using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>The host must not start with the test sign-in switched on in Production.</summary>
public sealed class E2eTestAuthStartupTests
{
    [Fact]
    public void TestAuthInProduction_StopsTheApplicationAtStartup()
    {
        using var factory = new E2eProductionApiFactory();

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(E2eTestAuthSettings.FlagKey, error.ToString());
        Assert.Contains("Production", error.ToString());
    }
}

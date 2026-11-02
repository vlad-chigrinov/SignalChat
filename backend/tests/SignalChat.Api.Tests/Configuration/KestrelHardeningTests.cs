using Microsoft.AspNetCore.Server.Kestrel.Core;
using SignalChat.Api.Configuration;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>The real Kestrel server writes the Server header by default; the host turns it off. TestServer does not emit it, so this checks the setting itself.</summary>
public sealed class KestrelHardeningTests
{
    [Fact]
    public void Default_KestrelWouldSendTheServerHeader()
    {
        Assert.True(new KestrelServerOptions().AddServerHeader);
    }

    [Fact]
    public void Apply_TurnsTheServerHeaderOff()
    {
        var options = new KestrelServerOptions();

        KestrelHardening.Apply(options);

        Assert.False(options.AddServerHeader);
    }
}

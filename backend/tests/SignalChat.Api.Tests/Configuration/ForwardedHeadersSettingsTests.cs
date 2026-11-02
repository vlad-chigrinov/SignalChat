using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using SignalChat.Api.Configuration;

namespace SignalChat.Api.Tests.Configuration;

public sealed class ForwardedHeadersSettingsTests
{
    private const int DefaultTrustedNetworkCount = 3;

    [Fact]
    public void Default_TrustsThePrivateDockerRanges()
    {
        var settings = Load(new Dictionary<string, string?>());

        Assert.Equal(DefaultTrustedNetworkCount, settings.TrustedNetworks.Count);
        Assert.Contains(settings.TrustedNetworks, network => network.Contains(IPAddress.Parse("172.18.0.2")));
        Assert.DoesNotContain(settings.TrustedNetworks, network => network.Contains(IPAddress.Loopback));
    }

    [Fact]
    public void Configured_ListsAddressesAndRanges()
    {
        var settings = Load(new Dictionary<string, string?> { [ForwardedHeadersSettings.TrustedProxiesKey] = "127.0.0.1/32, ::1" });

        Assert.Equal(2, settings.TrustedNetworks.Count);
        Assert.Contains(settings.TrustedNetworks, network => network.Contains(IPAddress.Loopback));
    }

    [Fact]
    public void ApplyTo_ReplacesTheDefaultsWithTheForwardedHeaderSet()
    {
        var settings = Load(new Dictionary<string, string?> { [ForwardedHeadersSettings.TrustedProxiesKey] = "10.0.0.1" });
        var options = new ForwardedHeadersOptions();

        settings.ApplyTo(options);

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
    }

    [Fact]
    public void InvalidEntry_StopsTheHostWithTheSettingName()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Load(new Dictionary<string, string?> { [ForwardedHeadersSettings.TrustedProxiesKey] = "not-an-address" }));

        Assert.Contains(ForwardedHeadersSettings.TrustedProxiesKey, error.Message);
    }

    private static ForwardedHeadersSettings Load(Dictionary<string, string?> values) =>
        ForwardedHeadersSettings.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
}

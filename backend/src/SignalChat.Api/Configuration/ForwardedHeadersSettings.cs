using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using IPNetwork = System.Net.IPNetwork;

namespace SignalChat.Api.Configuration;

/// <summary>
/// Which proxies may set <c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c> and <c>X-Forwarded-Host</c>. Read from
/// <c>FORWARDED_TRUSTED_PROXIES</c> (comma-separated addresses or CIDR ranges). The default is the private ranges
/// Docker networks use, where Traefik runs. Headers from any other address are ignored, so a client cannot fake its
/// scheme or host.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string TrustedProxiesKey = "FORWARDED_TRUSTED_PROXIES";

    public const string DefaultTrustedProxies = "10.0.0.0/8,172.16.0.0/12,192.168.0.0/16";

    private const char ListSeparator = ',';
    private const int IPv4HostPrefixLength = 32;
    private const int IPv6HostPrefixLength = 128;

    private ForwardedHeadersSettings(IReadOnlyCollection<IPNetwork> trustedNetworks)
    {
        TrustedNetworks = trustedNetworks;
    }

    public IReadOnlyCollection<IPNetwork> TrustedNetworks { get; }

    public static ForwardedHeadersSettings FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration[TrustedProxiesKey];
        var entries = (string.IsNullOrWhiteSpace(configured) ? DefaultTrustedProxies : configured)
            .Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ForwardedHeadersSettings(entries.Select(ParseNetwork).ToList());
    }

    public void ApplyTo(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var network in TrustedNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    /// <summary>A CIDR range as written, or a single address as a network of one host.</summary>
    private static IPNetwork ParseNetwork(string entry)
    {
        if (IPNetwork.TryParse(entry, out var network))
        {
            return network;
        }

        if (IPAddress.TryParse(entry, out var address))
        {
            var hostPrefixLength = address.AddressFamily == AddressFamily.InterNetworkV6 ? IPv6HostPrefixLength : IPv4HostPrefixLength;
            return new IPNetwork(address, hostPrefixLength);
        }

        throw new InvalidOperationException(
            $"{TrustedProxiesKey} must list IP addresses or CIDR ranges, got '{entry}'.");
    }
}

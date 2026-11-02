using SignalChat.Api.Hubs;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Keep-alive values must fit the production proxy: Traefik closes a connection idle for its idleTimeout.</summary>
public sealed class ChatHubKeepAliveTests
{
    /// <summary>Traefik idleTimeout on the websecure entrypoint, from the server's static configuration.</summary>
    private const int TraefikIdleTimeoutSeconds = 60;

    [Fact]
    public void KeepAlive_IsBelowTheProxyIdleTimeout()
    {
        Assert.True(ChatHubLimits.KeepAliveInterval < TimeSpan.FromSeconds(TraefikIdleTimeoutSeconds));
    }

    [Fact]
    public void ClientTimeout_IsLongerThanKeepAlive()
    {
        Assert.True(ChatHubLimits.ClientTimeoutInterval > ChatHubLimits.KeepAliveInterval);
    }
}

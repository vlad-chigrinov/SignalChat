namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>A loopback proxy in front of the API, trusted to set <c>X-Forwarded-*</c> as Traefik would.</summary>
public sealed class TrustedLoopbackProxyApiFactory : SignalChatApiFactory
{
    protected override string? ForwardedTrustedProxies => "127.0.0.1/32,::1/128";

    protected override string? SimulatedRemoteAddress => "127.0.0.1";
}

/// <summary>A client on a public address: not a trusted proxy, so its <c>X-Forwarded-*</c> headers must be ignored.</summary>
public sealed class PublicClientApiFactory : SignalChatApiFactory
{
    protected override string? SimulatedRemoteAddress => "203.0.113.10";
}

/// <summary>Serves the public domain only: any other <c>Host</c> header is refused.</summary>
public sealed class PublicHostApiFactory : SignalChatApiFactory
{
    public const string PublicDomain = "chat.vlados.tech";

    protected override string AllowedHosts => PublicDomain;
}

/// <summary>Production without a key ring directory: the host must refuse to start.</summary>
public sealed class ProductionWithoutKeysApiFactory : SignalChatApiFactory
{
    protected override string EnvironmentName => "Production";
}

/// <summary>Production with a key ring directory but no object store settings: the host must refuse to start.</summary>
public sealed class ProductionWithoutObjectStoreApiFactory : ProductionWithKeysApiFactory
{
    protected override bool ObjectStoreConfigured => false;
}

/// <summary>Production with a key ring directory on a volume: the host starts and answers.</summary>
public class ProductionWithKeysApiFactory : SignalChatApiFactory
{
    protected override string EnvironmentName => "Production";

    protected override string? DataProtectionKeysPath { get; } =
        Path.Combine(Path.GetTempPath(), "signalchat-keys-" + Guid.NewGuid().ToString("N"));
}

using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace SignalChat.Api.Configuration;

/// <summary>Kestrel settings for a public deployment: nothing in a response tells the server's software and version.</summary>
public static class KestrelHardening
{
    public static void Apply(KestrelServerOptions options)
    {
        options.AddServerHeader = false;
    }
}

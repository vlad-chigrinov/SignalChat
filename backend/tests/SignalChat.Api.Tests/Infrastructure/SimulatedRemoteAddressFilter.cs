using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>
/// Sets the client address of every request. <c>TestServer</c> has no remote address, and ASP.NET then trusts
/// forwarded headers from anyone; a test that checks proxy trust needs a real address to compare against.
/// </summary>
public sealed class SimulatedRemoteAddressFilter(IPAddress address) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = address;
            return nextMiddleware();
        });
        next(app);
    };
}

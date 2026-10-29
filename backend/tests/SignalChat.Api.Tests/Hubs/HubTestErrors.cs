using HubException = Microsoft.AspNetCore.SignalR.HubException;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>
/// SignalR replaces a hub error message with "An unexpected error occurred ... HubException: {code}" on the
/// client. The stable code is the text after the last ": ", so tests compare that part.
/// </summary>
internal static class HubTestErrors
{
    private const string Separator = ": ";

    public static string CodeOf(HubException error)
    {
        var index = error.Message.LastIndexOf(Separator, StringComparison.Ordinal);
        return index < 0 ? error.Message : error.Message[(index + Separator.Length)..];
    }
}

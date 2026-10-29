using Microsoft.AspNetCore.SignalR;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>Builds the <see cref="HubException"/> a client sees; its message is the stable error code.</summary>
public static class HubErrors
{
    public static HubException Of(string code) => new(code);

    public static HubException NotFound() => Of(HubErrorCodes.NotFound);

    public static HubException From(ChatRuleException rule) => Of(rule.Code);
}

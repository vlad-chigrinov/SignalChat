namespace SignalChat.Domain.Chat;

/// <summary>
/// A chat business rule was broken. <see cref="Code"/> is the stable error code: the REST API returns it
/// in ProblemDetails, the hub returns it as the HubException message.
/// </summary>
public sealed class ChatRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

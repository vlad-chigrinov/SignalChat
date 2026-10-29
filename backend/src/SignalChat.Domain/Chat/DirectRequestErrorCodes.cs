namespace SignalChat.Domain.Chat;

/// <summary>Stable error codes of the consent-based direct messages. REST and the hub both return them.</summary>
public static class DirectRequestErrorCodes
{
    /// <summary>The caller sent a request that the peer has not answered yet, or that the peer declined.</summary>
    public const string RequestPending = "dm_request_pending";

    /// <summary>The peer has sent the caller a request that the caller has not answered yet.</summary>
    public const string IncomingRequestExists = "dm_incoming_request_exists";

    /// <summary>The caller already has the maximum number of outgoing requests waiting for an answer.</summary>
    public const string RequestLimitReached = "dm_request_limit_reached";

    /// <summary>The conversation is only a request: its initiator may read it but not write to it yet.</summary>
    public const string NotAccepted = "dm_not_accepted";
}

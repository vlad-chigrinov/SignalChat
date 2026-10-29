namespace SignalChat.Api.Chat;

/// <summary>HTTP status of each stable chat error code.</summary>
public static class ChatErrorStatus
{
    private const int BadRequest = StatusCodes.Status400BadRequest;
    private const int NotFound = StatusCodes.Status404NotFound;
    private const int Conflict = StatusCodes.Status409Conflict;

    public static int For(string code) => code switch
    {
        "not_found" => NotFound,
        "room_name_taken" or "room_limit_reached" or "owner_cannot_leave" => Conflict,
        _ => BadRequest,
    };
}

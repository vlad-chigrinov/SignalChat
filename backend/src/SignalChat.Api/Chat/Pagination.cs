using SignalChat.Domain.Chat;

namespace SignalChat.Api.Chat;

public static class Pagination
{
    /// <summary>
    /// The page size a request asks for: the default when absent, capped at the maximum, and an error
    /// below one (a negative or zero page is not a page).
    /// </summary>
    public static int Clamp(int? requested, int defaultLimit, int maxLimit)
    {
        if (requested is null)
        {
            return defaultLimit;
        }
        if (requested < 1)
        {
            throw new ChatRuleException("invalid_limit", "limit must be at least 1.");
        }
        return Math.Min(requested.Value, maxLimit);
    }
}

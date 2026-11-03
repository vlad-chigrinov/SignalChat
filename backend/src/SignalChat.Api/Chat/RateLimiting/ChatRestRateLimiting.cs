using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>
/// Fixed-window limits on the REST routes the spec limits. The window is one minute. Each policy keeps its own
/// counters, so exhausting one route does not spend another route's allowance.
/// </summary>
public static class ChatRestRateLimiting
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddChatRestRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(ChatRateLimitOptions.SectionName).Get<ChatRateLimitOptions>()
            ?? new ChatRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = ChatRateLimitRejection.WriteAsync;
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.UserSearch, limits.UserSearchPerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomDiscover, limits.RoomDiscoverPerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomMembership, limits.RoomMembershipPerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(
                options, ChatRateLimitPolicies.DirectConversationCreate, limits.DirectConversationCreatePerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomCreate, limits.RoomCreatePerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.ProfileUpdate, limits.ProfileUpdatePerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.PhotoUpload, limits.PhotoUploadPerMinute, ChatRateLimitKey.Of);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.TestLogin, limits.TestLoginPerMinute, ChatRateLimitKey.OfAddress);
        });
        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options, string policy, int permitsPerMinute, Func<HttpContext, string> keyOf) =>
        options.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(
            keyOf(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = Window,
                QueueLimit = 0,
            }));
}

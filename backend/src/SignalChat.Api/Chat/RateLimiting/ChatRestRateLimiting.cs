using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace SignalChat.Api.Chat.RateLimiting;

/// <summary>
/// Per-user fixed-window limits on the REST routes the spec limits. The window is one minute. Each policy keeps its
/// own counters, so exhausting one route does not spend another route's allowance.
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
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.UserSearch, limits.UserSearchPerMinute);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomDiscover, limits.RoomDiscoverPerMinute);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomMembership, limits.RoomMembershipPerMinute);
            AddFixedWindowPolicy(
                options, ChatRateLimitPolicies.DirectConversationCreate, limits.DirectConversationCreatePerMinute);
            AddFixedWindowPolicy(options, ChatRateLimitPolicies.RoomCreate, limits.RoomCreatePerMinute);
        });
        return services;
    }

    private static void AddFixedWindowPolicy(RateLimiterOptions options, string policy, int permitsPerMinute) =>
        options.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(
            ChatRateLimitKey.Of(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = Window,
                QueueLimit = 0,
            }));
}

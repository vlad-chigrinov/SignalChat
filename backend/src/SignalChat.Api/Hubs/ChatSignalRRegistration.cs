using Microsoft.AspNetCore.SignalR;
namespace SignalChat.Api.Hubs;

/// <summary>The single place where the real-time chat is registered (hub, limits, presence, filters).</summary>
public static class ChatSignalRRegistration
{
    public static IServiceCollection AddChatSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = ChatHubLimits.MaxReceiveMessageSizeBytes;
            options.EnableDetailedErrors = false;
        });

        // Scale-out extension point: a Redis backplane (AddStackExchangeRedis) belongs here, behind config. Not enabled.

        services.AddSingleton<IHubConnectionRegistry, HubConnectionRegistry>();
        services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
        services.AddSingleton<IMessageRateLimiter, SlidingWindowMessageRateLimiter>();
        services.AddSingleton<ITypingThrottle, InMemoryTypingThrottle>();
        services.AddSingleton<PresenceNotifier>();
        services.AddScoped<HubGroupMembership>();
        services.AddScoped<HubSessionGuard>();
        services.AddScoped<ChatMessaging>();
        services.AddScoped<ConversationJoin>();
        return services;
    }
}

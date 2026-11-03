using Microsoft.Extensions.DependencyInjection;

namespace SignalChat.Infrastructure.Chat;

public static class ChatServiceRegistration
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddScoped<IConversationAccess, ConversationAccess>();
        services.AddScoped<IPeerDirectory, PeerDirectory>();
        services.AddScoped<IMessageStore, MessageStore>();
        services.AddScoped<IPhotoMessageService, PhotoMessageService>();
        services.AddScoped<IAttachmentReader, AttachmentReader>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IDirectConversationService, DirectConversationService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        return services;
    }
}

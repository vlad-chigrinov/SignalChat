using SignalChat.Domain.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>SignalR group names. Built only here, so the format lives in one place.</summary>
public static class ChatGroups
{
    private const string RoomPrefix = "room:";
    private const string DirectPrefix = "dm:";
    private const string UserPrefix = "user:";
    private const string GuidFormat = "D";

    public static string Room(Guid roomId) => RoomPrefix + roomId.ToString(GuidFormat);

    public static string Direct(Guid directConversationId) => DirectPrefix + directConversationId.ToString(GuidFormat);

    /// <summary>Every connection of a user joins this group on connect; presence is sent to it.</summary>
    public static string User(Guid userId) => UserPrefix + userId.ToString(GuidFormat);

    public static string For(ConversationRef conversation) =>
        conversation.Kind == ConversationKind.Room ? Room(conversation.Id) : Direct(conversation.Id);
}

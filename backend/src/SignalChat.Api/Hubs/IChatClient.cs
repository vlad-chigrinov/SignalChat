using SignalChat.Api.Chat.Dto;

namespace SignalChat.Api.Hubs;

/// <summary>Server-to-client events of <see cref="ChatHub"/>. Method names are the wire event names.</summary>
public interface IChatClient
{
    Task MessageReceived(MessageDto message);

    Task UserJoined(ConversationMemberEventDto memberEvent);

    Task UserLeft(ConversationMemberEventDto memberEvent);

    Task UserTyping(UserTypingEventDto typingEvent);

    Task PresenceChanged(PresenceChangedDto presenceEvent);
}

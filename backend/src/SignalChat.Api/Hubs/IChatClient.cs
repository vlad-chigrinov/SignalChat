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

    Task DirectRequestReceived(DirectRequestReceivedDto request);

    Task DirectRequestAccepted(DirectRequestAcceptedDto accepted);

    /// <summary>Someone became a member of a room (sent to the connections that have the room open).</summary>
    Task RoomMemberJoined(RoomMemberJoinedDto memberEvent);

    /// <summary>The user was added to a room by an invitation (sent to the invitee's personal group).</summary>
    Task AddedToRoom(AddedToRoomDto addedEvent);

    /// <summary>A user with a shared conversation (or the caller, in another tab) changed their name or avatar.</summary>
    Task UserProfileChanged(UserSummaryDto user);
}

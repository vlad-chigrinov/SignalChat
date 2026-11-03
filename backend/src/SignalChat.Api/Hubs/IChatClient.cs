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

    /// <summary>A user with a shared conversation (or the caller, in another tab) changed their name or avatar.</summary>
    Task UserProfileChanged(UserSummaryDto user);

    /// <summary>The reactions of a message changed: the new counts, for every member of the conversation.</summary>
    Task ReactionsChanged(ReactionsChangedEventDto change);

    /// <summary>The caller's own reaction on a message changed (sent to the caller's tabs only).</summary>
    Task MyReactionChanged(MyReactionChangedEventDto change);
}

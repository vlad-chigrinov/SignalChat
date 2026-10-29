using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat;
using SignalChat.Api.Chat.Dto;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Hubs;

/// <summary>
/// Real-time chat hub (SignalR over WSS). Authenticated by the session cookie; <c>Context.User</c> carries the
/// internal user id. Membership is checked in the database before a group join and before every send.
/// The send path lives in <see cref="ChatMessaging"/>.
/// </summary>
[Authorize]
public sealed class ChatHub(
    IHubConnectionRegistry connections,
    HubSessionGuard sessionGuard,
    IPresenceTracker presence,
    PresenceNotifier presenceNotifier,
    IConversationAccess access,
    IUserDirectory users,
    ChatMessaging messaging,
    ITypingThrottle typingThrottle) : Hub<IChatClient>
{
    public const string Route = "/hubs/chat";

    public override async Task OnConnectedAsync()
    {
        if (!SessionPrincipal.TryGetUserId(Context.User!, out var userId))
        {
            Context.Abort();
            return;
        }

        // Register first, then re-check the session. A logout that lands before the check aborts this connection
        // (it is already registered); one that lands after the check finds it in the registry. No window is left.
        connections.Register(Context.UserIdentifier!, Context);
        if (!await sessionGuard.IsStillValidAsync(Context.User!, Context.ConnectionAborted))
        {
            connections.Unregister(Context.UserIdentifier!, Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.User(userId));
        if (presence.ConnectionOpened(userId))
        {
            await presenceNotifier.NotifyAsync(userId, isOnline: true, Context.ConnectionAborted);
        }
        ChatConnectionState.MarkPresenceOpened(Context);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdentifier = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userIdentifier))
        {
            connections.Unregister(userIdentifier, Context.ConnectionId);
            if (ChatConnectionState.TakePresenceOpened(Context)
                && SessionPrincipal.TryGetUserId(Context.User!, out var userId)
                && presence.ConnectionClosed(userId))
            {
                await presenceNotifier.NotifyAsync(userId, isOnline: false, CancellationToken.None);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Joins the conversation's group. Idempotent. Other members are told with <c>UserJoined</c>.</summary>
    public async Task JoinRoom(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        var joined = ChatConnectionState.Joined(Context);
        if (joined.ContainsKey(id))
        {
            return;
        }

        if (joined.Count >= ChatHubLimits.MaxJoinedConversationsPerConnection)
        {
            throw HubErrors.Of(HubErrorCodes.TooManyGroups);
        }

        var userId = CurrentUserId();
        var conversation = await FindMemberAsync(userId, id);
        var group = ChatGroups.For(conversation);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        joined[id] = conversation;

        await Clients.OthersInGroup(group).UserJoined(
            new ConversationMemberEventDto(id, await CurrentUserSummaryAsync(userId)));
    }

    /// <summary>Leaves the conversation's group. A no-op when the connection had not joined it.</summary>
    public async Task LeaveRoom(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        if (!ChatConnectionState.Joined(Context).Remove(id, out var conversation))
        {
            return;
        }

        var group = ChatGroups.For(conversation);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        await Clients.OthersInGroup(group).UserLeft(
            new ConversationMemberEventDto(id, await CurrentUserSummaryAsync(CurrentUserId())));
    }

    /// <summary>Sends a message: membership, rate limit and save happen in <see cref="ChatMessaging"/>.</summary>
    public Task<MessageDto> SendMessage(string conversationId, string text) =>
        messaging.SendAsync(CurrentUserId(), ParseConversationId(conversationId), text, Context.ConnectionAborted);

    /// <summary>
    /// Typing hint to the other members. Only for conversations this connection joined (no database read),
    /// and repeated signals inside the throttle window are dropped silently.
    /// </summary>
    public async Task Typing(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        if (!ChatConnectionState.Joined(Context).TryGetValue(id, out var conversation))
        {
            throw HubErrors.NotFound();
        }

        var userId = CurrentUserId();
        if (!typingThrottle.TryPass(userId, id))
        {
            return;
        }

        await Clients.OthersInGroup(ChatGroups.For(conversation)).UserTyping(new UserTypingEventDto(id, userId));
    }

    private Guid CurrentUserId() =>
        SessionPrincipal.TryGetUserId(Context.User!, out var userId)
            ? userId
            : throw HubErrors.Of(HubErrorCodes.Unauthenticated);

    /// <summary>A malformed id is "not found", never a validation message: the caller learns nothing about ids.</summary>
    private static Guid ParseConversationId(string conversationId) =>
        Guid.TryParse(conversationId, out var id) ? id : throw HubErrors.NotFound();

    /// <summary>The single membership check of the hub, the same one REST uses.</summary>
    private async Task<ConversationRef> FindMemberAsync(Guid userId, Guid conversationId) =>
        await access.FindForMemberAsync(userId, conversationId, Context.ConnectionAborted)
        ?? throw HubErrors.NotFound();

    private async Task<UserSummaryDto> CurrentUserSummaryAsync(Guid userId)
    {
        var user = await users.FindPublicAsync(userId, Context.ConnectionAborted)
            ?? throw HubErrors.NotFound();
        return ChatMapping.ToDto(user);
    }
}

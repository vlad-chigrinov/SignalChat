using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    PresenceAnnouncer presence,
    IConversationAccess access,
    IUserDirectory users,
    ChatMessaging messaging,
    ConversationJoin joins,
    IMembershipChangeRateLimiter membershipLimiter,
    ITypingThrottle typingThrottle,
    ILogger<ChatHub> logger) : Hub<IChatClient>
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
        var joined = new ConcurrentDictionary<Guid, ConversationRef>();
        if (!connections.TryRegister(Context.UserIdentifier!, Context, joined, ChatHubLimits.MaxConnectionsPerUser))
        {
            // The open connections stay; only this new one is refused.
            logger.LogWarning("Hub connection refused: the user already has the maximum number of open connections.");
            Context.Abort();
            return;
        }

        ChatConnectionState.Attach(Context, joined);
        if (!await sessionGuard.IsStillValidAsync(Context.User!, Context.ConnectionAborted))
        {
            connections.Unregister(Context.UserIdentifier!, Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.User(userId));
        // Mark before counting: if the count is cancelled or the connection drops during it, the disconnect
        // still finds the mark and closes the presence count. Otherwise the user would stay "online" forever.
        ChatConnectionState.MarkPresenceOpened(Context);
        await presence.ConnectedAsync(userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdentifier = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userIdentifier))
        {
            connections.Unregister(userIdentifier, Context.ConnectionId);
            if (ChatConnectionState.TakePresenceOpened(Context)
                && SessionPrincipal.TryGetUserId(Context.User!, out var userId))
            {
                await presence.DisconnectedAsync(userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Joins the conversation's group. Idempotent. Other members are told with <c>UserJoined</c>.</summary>
    public async Task JoinRoom(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        var userId = CurrentUserId();
        if (!membershipLimiter.TryAcquire(userId))
        {
            throw HubErrors.Of(HubErrorCodes.RateLimited);
        }

        var joined = ChatConnectionState.Joined(Context);
        if (joined.ContainsKey(id))
        {
            return;
        }

        if (joined.Count >= ChatHubLimits.MaxJoinedConversationsPerConnection)
        {
            throw HubErrors.Of(HubErrorCodes.TooManyGroups);
        }

        var conversation = await joins.JoinAsync(
            userId, id, Context.ConnectionId, Groups, joined, Context.ConnectionAborted);

        await Clients.OthersInGroup(ChatGroups.For(conversation)).UserJoined(
            new ConversationMemberEventDto(id, await CurrentUserSummaryAsync(userId)));
    }

    /// <summary>Leaves the conversation's group. A no-op when the connection had not joined it.</summary>
    public async Task LeaveRoom(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        if (!membershipLimiter.TryAcquire(CurrentUserId()))
        {
            throw HubErrors.Of(HubErrorCodes.RateLimited);
        }

        if (!ChatConnectionState.Joined(Context).TryRemove(id, out var conversation))
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
    /// Typing hint to the other members. Needs a join on this connection, and the membership is checked in the
    /// database again, so a user who left over REST stops being heard. Repeated signals inside the throttle
    /// window are dropped silently.
    /// </summary>
    public async Task Typing(string conversationId)
    {
        var id = ParseConversationId(conversationId);
        if (!ChatConnectionState.Joined(Context).ContainsKey(id))
        {
            throw HubErrors.NotFound();
        }

        var userId = CurrentUserId();
        if (!typingThrottle.TryPass(userId, id))
        {
            return;
        }

        var conversation = await access.FindForMemberAsync(userId, id, Context.ConnectionAborted)
            ?? throw HubErrors.NotFound();
        await Clients.OthersInGroup(ChatGroups.For(conversation)).UserTyping(new UserTypingEventDto(id, userId));
    }

    private Guid CurrentUserId() =>
        SessionPrincipal.TryGetUserId(Context.User!, out var userId)
            ? userId
            : throw HubErrors.Of(HubErrorCodes.Unauthenticated);

    /// <summary>A malformed id is "not found", never a validation message: the caller learns nothing about ids.</summary>
    private static Guid ParseConversationId(string conversationId) =>
        Guid.TryParse(conversationId, out var id) ? id : throw HubErrors.NotFound();

    private async Task<UserSummaryDto> CurrentUserSummaryAsync(Guid userId)
    {
        var user = await users.FindPublicAsync(userId, Context.ConnectionAborted)
            ?? throw HubErrors.NotFound();
        return ChatMapping.ToDto(user);
    }
}

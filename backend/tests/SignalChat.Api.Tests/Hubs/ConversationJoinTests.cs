using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Hubs;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using HubException = Microsoft.AspNetCore.SignalR.HubException;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>
/// The join path without a live race: the membership answers are scripted, so a REST leave between the first
/// check and the group add can be simulated exactly.
/// </summary>
public sealed class ConversationJoinTests
{
    private const string ConnectionId = "connection-1";

    [Fact]
    public async Task StillAMember_JoinsTheGroup()
    {
        var conversation = new ConversationRef(ConversationKind.Room, Guid.NewGuid());
        var access = new ScriptedConversationAccess(conversation, conversation);
        var groups = new RecordingGroupManager();
        var joined = new ConcurrentDictionary<Guid, ConversationRef>();

        var result = await new ConversationJoin(access).JoinAsync(
            Guid.NewGuid(), conversation.Id, ConnectionId, groups, joined, CancellationToken.None);

        Assert.Equal(conversation, result);
        Assert.Equal([ChatGroups.For(conversation)], groups.Added);
        Assert.Empty(groups.Removed);
        Assert.Equal(conversation, joined[conversation.Id]);
    }

    [Fact]
    public async Task NotAMember_IsNotFound_AndNothingIsAdded()
    {
        var access = new ScriptedConversationAccess([null]);
        var groups = new RecordingGroupManager();
        var joined = new ConcurrentDictionary<Guid, ConversationRef>();

        var error = await Assert.ThrowsAsync<HubException>(() => new ConversationJoin(access).JoinAsync(
            Guid.NewGuid(), Guid.NewGuid(), ConnectionId, groups, joined, CancellationToken.None));

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(error));
        Assert.Empty(groups.Added);
        Assert.Empty(joined);
    }

    [Fact]
    public async Task LeaveBetweenTheCheckAndTheAdd_RollsTheJoinBack()
    {
        var conversation = new ConversationRef(ConversationKind.Room, Guid.NewGuid());
        var access = new ScriptedConversationAccess(conversation, null);
        var groups = new RecordingGroupManager();
        var joined = new ConcurrentDictionary<Guid, ConversationRef>();

        var error = await Assert.ThrowsAsync<HubException>(() => new ConversationJoin(access).JoinAsync(
            Guid.NewGuid(), conversation.Id, ConnectionId, groups, joined, CancellationToken.None));

        Assert.Equal(HubErrorCodes.NotFound, HubTestErrors.CodeOf(error));
        Assert.Equal([ChatGroups.For(conversation)], groups.Added);
        Assert.Equal([ChatGroups.For(conversation)], groups.Removed);
        Assert.Empty(joined);
    }

    /// <summary>Answers the membership checks in order; the last answer repeats.</summary>
    private sealed class ScriptedConversationAccess(params ConversationRef?[] answers) : IConversationAccess
    {
        private int _next;

        public Task<ConversationRef?> FindForMemberAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
            NextAnswer();

        public Task<ConversationRef?> FindActiveAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
            NextAnswer();

        private Task<ConversationRef?> NextAnswer()
        {
            var answer = answers[Math.Min(_next, answers.Length - 1)];
            _next++;
            return Task.FromResult(answer);
        }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> Added { get; } = [];

        public List<string> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add(groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add(groupName);
            return Task.CompletedTask;
        }
    }
}

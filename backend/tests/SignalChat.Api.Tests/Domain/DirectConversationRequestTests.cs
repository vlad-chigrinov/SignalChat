using SignalChat.Domain.Chat;

namespace SignalChat.Api.Tests.Domain;

/// <summary>The request states of a direct conversation: who may answer, and which transitions exist.</summary>
public sealed class DirectConversationRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_StartsPending_WithTheInitiator()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var conversation = DirectConversation.Request(alice, bob, Now);

        Assert.Equal(DirectConversationStatus.Pending, conversation.Status);
        Assert.Equal(alice, conversation.InitiatorId);
        Assert.Null(conversation.RespondedAt);
        Assert.True(conversation.IsResponder(bob));
        Assert.False(conversation.IsResponder(alice));
    }

    [Fact]
    public void Accept_ByTheResponder_MakesItAChat()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var conversation = DirectConversation.Request(alice, bob, Now);

        conversation.Accept(bob, Now.AddMinutes(1));

        Assert.Equal(DirectConversationStatus.Accepted, conversation.Status);
        Assert.Equal(Now.AddMinutes(1), conversation.RespondedAt);
    }

    [Fact]
    public void Accept_ByTheInitiator_OrAnAnswerTwice_IsNotFound()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var conversation = DirectConversation.Request(alice, bob, Now);

        var byInitiator = Assert.Throws<ChatRuleException>(() => conversation.Accept(alice, Now));
        conversation.Accept(bob, Now);
        var twice = Assert.Throws<ChatRuleException>(() => conversation.Accept(bob, Now));

        Assert.Equal("not_found", byInitiator.Code);
        Assert.Equal("not_found", twice.Code);
    }

    [Fact]
    public void Accept_ByAnOutsider_IsNotFound()
    {
        var conversation = DirectConversation.Request(Guid.NewGuid(), Guid.NewGuid(), Now);

        var error = Assert.Throws<ChatRuleException>(() => conversation.Accept(Guid.NewGuid(), Now));

        Assert.Equal("not_found", error.Code);
    }

    [Fact]
    public void Decline_ThenReopen_ByTheDecliner_MakesItANewRequestFromThem()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var conversation = DirectConversation.Request(alice, bob, Now);

        conversation.Decline(bob, Now);
        conversation.Reopen(bob);

        Assert.Equal(DirectConversationStatus.Pending, conversation.Status);
        Assert.Equal(bob, conversation.InitiatorId);
        Assert.Null(conversation.RespondedAt);
    }

    [Fact]
    public void Reopen_ByTheInitiator_OrWhenNotDeclined_IsNotFound()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var pending = DirectConversation.Request(alice, bob, Now);
        var declined = DirectConversation.Request(alice, bob, Now);
        declined.Decline(bob, Now);

        var notDeclined = Assert.Throws<ChatRuleException>(() => pending.Reopen(bob));
        var byInitiator = Assert.Throws<ChatRuleException>(() => declined.Reopen(alice));

        Assert.Equal("not_found", notDeclined.Code);
        Assert.Equal("not_found", byInitiator.Code);
    }
}

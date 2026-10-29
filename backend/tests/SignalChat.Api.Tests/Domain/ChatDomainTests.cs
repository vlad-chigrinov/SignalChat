using SignalChat.Domain.Chat;

namespace SignalChat.Api.Tests.Domain;

public sealed class ChatDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Room_TrimsTheName_AndNormalizesItForUniqueness()
    {
        var room = Room.Create("  General Chat  ", null, Guid.NewGuid(), Now);

        Assert.Equal("General Chat", room.Name);
        Assert.Equal("general chat", room.NormalizedName);
        Assert.Equal(string.Empty, room.Description);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("   ")]
    public void Room_RejectsNamesShorterThanTheMinimum(string name)
    {
        var error = Assert.Throws<ChatRuleException>(() => Room.Create(name, null, Guid.NewGuid(), Now));

        Assert.Equal("invalid_room_name", error.Code);
    }

    [Fact]
    public void Room_AcceptsNamesAtTheBoundaries_AndRejectsOneOverMaximum()
    {
        var shortest = new string('a', Room.NameMinLength);
        var longest = new string('b', Room.NameMaxLength);

        Assert.Equal(shortest, Room.Create(shortest, null, Guid.NewGuid(), Now).Name);
        Assert.Equal(longest, Room.Create(longest, null, Guid.NewGuid(), Now).Name);
        var error = Assert.Throws<ChatRuleException>(() =>
            Room.Create(new string('c', Room.NameMaxLength + 1), null, Guid.NewGuid(), Now));
        Assert.Equal("invalid_room_name", error.Code);
    }

    [Fact]
    public void Room_RejectsDescriptionOverTheLimit()
    {
        var error = Assert.Throws<ChatRuleException>(() =>
            Room.Create("Valid name", new string('d', Room.DescriptionMaxLength + 1), Guid.NewGuid(), Now));

        Assert.Equal("invalid_room_description", error.Code);
    }

    [Fact]
    public void DirectConversation_IsTheSameForBothOrders()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var forward = DirectConversation.Request(alice, bob, Now);
        var backward = DirectConversation.Request(bob, alice, Now);

        Assert.Equal(forward.UserLowId, backward.UserLowId);
        Assert.Equal(forward.UserHighId, backward.UserHighId);
        Assert.True(forward.UserLowId.CompareTo(forward.UserHighId) < 0);
    }

    [Fact]
    public void DirectConversation_RejectsConversationWithSelf()
    {
        var self = Guid.NewGuid();

        var error = Assert.Throws<ChatRuleException>(() => DirectConversation.Request(self, self, Now));

        Assert.Equal("self_conversation", error.Code);
    }

    [Fact]
    public void DirectConversation_PeerOf_ReturnsTheOtherParticipant()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var conversation = DirectConversation.Request(alice, bob, Now);

        Assert.Equal(bob, conversation.PeerOf(alice));
        Assert.Equal(alice, conversation.PeerOf(bob));
        Assert.True(conversation.Includes(alice));
        Assert.False(conversation.Includes(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => conversation.PeerOf(Guid.NewGuid()));
    }

    [Fact]
    public void Message_TrimsText_AndBelongsToExactlyOneConversation()
    {
        var roomId = Guid.NewGuid();
        var author = Guid.NewGuid();

        var message = Message.InRoom(roomId, author, "  hello  ", Now);

        Assert.Equal("hello", message.Text);
        Assert.Equal(roomId, message.RoomId);
        Assert.Null(message.DirectConversationId);
        Assert.Equal(roomId, message.ConversationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Message_RejectsEmptyText(string? text)
    {
        var error = Assert.Throws<ChatRuleException>(() => Message.InRoom(Guid.NewGuid(), Guid.NewGuid(), text, Now));

        Assert.Equal("invalid_message", error.Code);
    }

    [Fact]
    public void Message_AcceptsTheMaximumLength_AndRejectsOneMore()
    {
        var author = Guid.NewGuid();
        var conversation = Guid.NewGuid();

        Assert.Equal(Message.MaxTextLength, Message.InDirectConversation(conversation, author, new string('x', Message.MaxTextLength), Now).Text.Length);
        var error = Assert.Throws<ChatRuleException>(() =>
            Message.InDirectConversation(conversation, author, new string('x', Message.MaxTextLength + 1), Now));
        Assert.Equal("invalid_message", error.Code);
    }

    [Fact]
    public void Message_Delete_IsSoftAndIdempotent()
    {
        var message = Message.InRoom(Guid.NewGuid(), Guid.NewGuid(), "to be removed", Now);
        var firstDeletion = Now.AddMinutes(1);

        message.Delete(firstDeletion);
        message.Delete(Now.AddMinutes(2));

        Assert.Equal(firstDeletion, message.DeletedAt);
    }
}

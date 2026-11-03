using Microsoft.EntityFrameworkCore;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;

namespace SignalChat.Api.Tests.Domain;

public sealed class MessageReactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The list is mirrored by <c>frontend/src/api/reactionCatalog.ts</c> and pinned there by a test too:
    /// change all three together.
    /// </summary>
    [Fact]
    public void TheCatalog_IsTheAgreedSmallList()
    {
        Assert.Equal(["👍", "❤️", "😂", "😮", "😢", "🎉"], ReactionCatalog.Emojis);
    }

    [Fact]
    public void TheCatalog_AllowsOnlyExactMembers()
    {
        Assert.True(ReactionCatalog.IsAllowed("👍"));
        Assert.False(ReactionCatalog.IsAllowed("👍👍"));
        Assert.False(ReactionCatalog.IsAllowed(" 👍"));
        Assert.False(ReactionCatalog.IsAllowed("<b>"));
        Assert.False(ReactionCatalog.IsAllowed(""));
        Assert.False(ReactionCatalog.IsAllowed(null));
    }

    [Fact]
    public void ANewReaction_RejectsAnEmojiOutsideTheCatalog()
    {
        var refusal = Assert.Throws<ChatRuleException>(
            () => MessageReaction.Create(Guid.NewGuid(), Guid.NewGuid(), "💩", Now));

        Assert.Equal("invalid_reaction", refusal.Code);
    }

    [Fact]
    public void ChangingAReaction_RejectsAnEmojiOutsideTheCatalog_AndKeepsTheOldOne()
    {
        var reaction = MessageReaction.Create(Guid.NewGuid(), Guid.NewGuid(), "👍", Now);

        Assert.Throws<ChatRuleException>(() => reaction.ChangeTo("<script>"));

        Assert.Equal("👍", reaction.Emoji);
    }

    [Fact]
    public void ChangingAReaction_KeepsTheMessageAndTheUser()
    {
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reaction = MessageReaction.Create(messageId, userId, "👍", Now);

        reaction.ChangeTo("❤️");

        Assert.Equal((messageId, userId, "❤️"), (reaction.MessageId, reaction.UserId, reaction.Emoji));
    }

    /// <summary>The rule does not live only in the service: the database refuses a second reaction of the same user on a message.</summary>
    [Fact]
    public async Task TheDatabase_RefusesASecondReactionOfTheSameUserOnAMessage()
    {
        using var factory = new SignalChatApiFactory();
        var (owner, cookie) = await factory.CreateSessionAsync(HubFixtures.Profile("rx-unique"));
        using var client = factory.CreateApiClient();
        var roomId = await HubFixtures.CreateRoomAsync(client, cookie);
        var messageId = await ReactionFixtures.AppendMessageAsync(factory, owner.Id, roomId);
        await ReactionDatabase.InsertAsync(factory, messageId, owner.Id, "👍");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => ReactionDatabase.InsertAsync(factory, messageId, owner.Id, "❤️"));

        Assert.Equal(1, await ReactionFixtures.StoredReactionCountAsync(factory, messageId));
    }
}

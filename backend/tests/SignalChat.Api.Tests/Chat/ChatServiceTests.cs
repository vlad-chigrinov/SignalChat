using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Chat;

/// <summary>The access rules as the hub (#6) will call them: no HTTP involved.</summary>
public sealed class ChatServiceTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task AppendAsync_ByNonMember_IsRefusedAsNotFound_AndStoresNothing()
    {
        var (owner, _) = await factory.CreateSessionAsync(Profile("svc-owner"));
        var (outsider, _) = await factory.CreateSessionAsync(Profile("svc-outsider"));
        var roomId = await CreateRoomAsync(owner.Id);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();
        var error = await Assert.ThrowsAsync<ChatRuleException>(() =>
            store.AppendAsync(outsider.Id, roomId, "let me in", CancellationToken.None));

        Assert.Equal("not_found", error.Code);
        var page = await store.GetPageAsync(new ConversationRef(ConversationKind.Room, roomId), null, 10, CancellationToken.None);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task AppendAsync_WithEmptyOrTooLongText_IsRefused()
    {
        var (owner, _) = await factory.CreateSessionAsync(Profile("svc-length"));
        var roomId = await CreateRoomAsync(owner.Id);
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();

        var empty = await Assert.ThrowsAsync<ChatRuleException>(() => store.AppendAsync(owner.Id, roomId, "   ", CancellationToken.None));
        var tooLong = await Assert.ThrowsAsync<ChatRuleException>(() =>
            store.AppendAsync(owner.Id, roomId, new string('x', Message.MaxTextLength + 1), CancellationToken.None));

        Assert.Equal("invalid_message", empty.Code);
        Assert.Equal("invalid_message", tooLong.Code);
    }

    [Fact]
    public async Task ConversationAccess_ReturnsKindForMembers_AndNullForOutsidersAndMissingIds()
    {
        var (alice, _) = await factory.CreateSessionAsync(Profile("acc-alice"));
        var (bob, _) = await factory.CreateSessionAsync(Profile("acc-bob"));
        var (outsider, _) = await factory.CreateSessionAsync(Profile("acc-outsider"));
        var roomId = await CreateRoomAsync(alice.Id);
        using var scope = factory.Services.CreateScope();
        var direct = scope.ServiceProvider.GetRequiredService<IDirectConversationService>();
        var conversation = await direct.GetOrCreateAsync(alice.Id, bob.Id, CancellationToken.None);
        var access = scope.ServiceProvider.GetRequiredService<IConversationAccess>();

        var roomForOwner = await access.FindForMemberAsync(alice.Id, roomId, CancellationToken.None);
        var directForPeer = await access.FindForMemberAsync(bob.Id, conversation.Id, CancellationToken.None);
        var roomForOutsider = await access.FindForMemberAsync(outsider.Id, roomId, CancellationToken.None);
        var directForOutsider = await access.FindForMemberAsync(outsider.Id, conversation.Id, CancellationToken.None);
        var missing = await access.FindForMemberAsync(alice.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(new ConversationRef(ConversationKind.Room, roomId), roomForOwner);
        Assert.Equal(new ConversationRef(ConversationKind.Direct, conversation.Id), directForPeer);
        Assert.Null(roomForOutsider);
        Assert.Null(directForOutsider);
        Assert.Null(missing);
    }

    [Fact]
    public async Task PeerDirectory_ListsUsersSharingARoomOrADirectConversation_Only()
    {
        var (alice, _) = await factory.CreateSessionAsync(Profile("peer-alice"));
        var (roommate, _) = await factory.CreateSessionAsync(Profile("peer-roommate"));
        var (dmPeer, _) = await factory.CreateSessionAsync(Profile("peer-dm"));
        var (stranger, _) = await factory.CreateSessionAsync(Profile("peer-stranger"));
        var roomId = await CreateRoomAsync(alice.Id);
        using var scope = factory.Services.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomService>();
        await rooms.JoinAsync(roommate.Id, roomId, CancellationToken.None);
        var direct = scope.ServiceProvider.GetRequiredService<IDirectConversationService>();
        await direct.GetOrCreateAsync(alice.Id, dmPeer.Id, CancellationToken.None);
        var peers = scope.ServiceProvider.GetRequiredService<IPeerDirectory>();

        var alicePeers = await peers.GetPeerIdsAsync(alice.Id, CancellationToken.None);

        Assert.Contains(roommate.Id, alicePeers);
        Assert.Contains(dmPeer.Id, alicePeers);
        Assert.DoesNotContain(stranger.Id, alicePeers);
        Assert.DoesNotContain(alice.Id, alicePeers);
    }

    private async Task<Guid> CreateRoomAsync(Guid ownerId)
    {
        using var scope = factory.Services.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var room = await rooms.CreateAsync(ownerId, ChatHttp.UniqueName("Service"), null, CancellationToken.None);
        return room.Id;
    }

    private static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", null);
}

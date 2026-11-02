using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Persistence.Seed;

namespace SignalChat.Api.Tests.Chat;

/// <summary>The stage demo data: rooms with history and an accepted direct chat, created once and never twice.</summary>
public sealed class DemoDataSeederTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    [Fact]
    public async Task SeedAsync_CreatesUsersRoomsMessagesAndAnAcceptedDirectChat()
    {
        await SeedAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        var seedUsers = await db.Users.Where(user => user.GoogleSubject.StartsWith(DemoDataSeeder.GoogleSubjectPrefix)).ToListAsync();

        Assert.Equal(DemoDataSeeder.UserCount, seedUsers.Count);
        Assert.True(await db.Rooms.CountAsync() >= DemoDataSeeder.RoomCount);
        Assert.True(await db.Messages.CountAsync(message => message.RoomId != null) > 0);
        var direct = await db.DirectConversations.SingleAsync();
        Assert.Equal(DirectConversationStatus.Accepted, direct.Status);
        Assert.True(await db.Messages.CountAsync(message => message.DirectConversationId == direct.Id) > 0);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_CreatesNothingTheSecondTime()
    {
        await SeedAsync();
        var first = await CountsAsync();

        await SeedAsync();
        var second = await CountsAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SeedAsync_MakesEverySeedUserAMemberOfTheGeneralRoom()
    {
        await SeedAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        var general = await db.Rooms.SingleAsync(room => room.NormalizedName == DemoDataSeeder.GeneralRoomName.ToLowerInvariant());
        var members = await db.RoomMembers.CountAsync(member => member.RoomId == general.Id);

        Assert.Equal(DemoDataSeeder.UserCount, members);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var seeder = new DemoDataSeeder(
            scope.ServiceProvider.GetRequiredService<SignalChatDbContext>(), TimeProvider.System);
        await seeder.SeedAsync(CancellationToken.None);
    }

    private async Task<(int Users, int Rooms, int Members, int Messages, int Directs)> CountsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        return (
            await db.Users.CountAsync(),
            await db.Rooms.CountAsync(),
            await db.RoomMembers.CountAsync(),
            await db.Messages.CountAsync(),
            await db.DirectConversations.CountAsync());
    }
}

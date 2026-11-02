using Microsoft.EntityFrameworkCore;
using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;

namespace SignalChat.Infrastructure.Persistence.Seed;

/// <summary>
/// Fills an empty stage database with demo people, rooms and messages, so a tester signing in
/// through Google sees a chat that is already alive. Idempotent: once the demo users exist it does nothing.
/// The demo users cannot sign in (their Google subject is not a real one); they only exist as chat partners.
/// </summary>
public sealed class DemoDataSeeder(SignalChatDbContext db, TimeProvider clock)
{
    public const string GoogleSubjectPrefix = "seed:";
    public const string GeneralRoomName = "General";
    public const string DemoEmailDomain = "seed.invalid";
    public const int UserCount = 4;
    public const int RoomCount = 3;

    private const string AliceName = "Alice Demo";
    private const string BobName = "Bob Demo";
    private const string CarolName = "Carol Demo";
    private const string DaveName = "Dave Demo";
    private const int MinutesBetweenMessages = 7;

    private readonly record struct DemoLine(string Author, string Text);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(user => user.GoogleSubject.StartsWith(GoogleSubjectPrefix), cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var users = CreateUsers(now);
        var general = Room.Create(GeneralRoomName, "Everyone starts here.", users[AliceName].Id, now);
        var dev = Room.Create("Dev talk", "Builds, bugs and deploys.", users[BobName].Id, now);
        var random = Room.Create("Random", "Anything that does not fit elsewhere.", users[CarolName].Id, now);

        db.Users.AddRange(users.Values);
        db.Rooms.AddRange(general, dev, random);
        db.RoomMembers.AddRange(MembersOf(general, users.Values, ownerId: general.CreatedByUserId, now));
        db.RoomMembers.AddRange(MembersOf(dev, users.Values.Take(3), ownerId: dev.CreatedByUserId, now));
        db.RoomMembers.AddRange(MembersOf(random, users.Values.Skip(1), ownerId: random.CreatedByUserId, now));
        db.Messages.AddRange(RoomMessages(general, users, now, GeneralLines));
        db.Messages.AddRange(RoomMessages(dev, users, now, DevLines));
        db.Messages.AddRange(RoomMessages(random, users, now, RandomLines));

        var direct = DirectConversation.Request(users[AliceName].Id, users[BobName].Id, now);
        direct.Accept(users[BobName].Id, now);
        db.DirectConversations.Add(direct);
        db.Messages.AddRange(DirectMessages(direct, users, now));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, User> CreateUsers(DateTimeOffset now)
    {
        var names = new[] { AliceName, BobName, CarolName, DaveName };
        return names.ToDictionary(
            name => name,
            name =>
            {
                var handle = name.Split(' ')[0].ToLowerInvariant();
                return User.RegisterFromGoogle($"{GoogleSubjectPrefix}{handle}", $"{handle}@{DemoEmailDomain}", name, avatarUrl: null, now);
            });
    }

    private static IEnumerable<RoomMember> MembersOf(Room room, IEnumerable<User> users, Guid ownerId, DateTimeOffset now) =>
        users.Select(user => RoomMember.Join(room.Id, user.Id, user.Id == ownerId ? RoomRole.Owner : RoomRole.Member, now));

    private static IEnumerable<Message> RoomMessages(
        Room room, Dictionary<string, User> users, DateTimeOffset now, DemoLine[] lines) =>
        Timeline(lines, now).Select(entry => Message.InRoom(room.Id, users[entry.Line.Author].Id, entry.Line.Text, entry.At));

    private static IEnumerable<Message> DirectMessages(
        DirectConversation direct, Dictionary<string, User> users, DateTimeOffset now) =>
        Timeline(DirectLines, now).Select(entry =>
            Message.InDirectConversation(direct.Id, users[entry.Line.Author].Id, entry.Line.Text, entry.At));

    /// <summary>Spreads the lines over the last hours, oldest first, the last one a few minutes ago.</summary>
    private static IEnumerable<(DemoLine Line, DateTimeOffset At)> Timeline(DemoLine[] lines, DateTimeOffset now) =>
        lines.Select((line, index) => (line, now.AddMinutes(-(lines.Length - index) * MinutesBetweenMessages)));

    private static readonly DemoLine[] GeneralLines =
    [
        new(AliceName, "Welcome to SignalChat stage! This is demo data from the seed."),
        new(BobName, "Hi all. Anything you write here lives only in the stage database."),
        new(CarolName, "Try creating a room and writing to someone in a direct chat."),
        new(DaveName, "Messages are plain text: <b>markup</b> is shown as it is."),
        new(AliceName, "Open the Swagger page of the stage API to see every endpoint."),
    ];

    private static readonly DemoLine[] DevLines =
    [
        new(BobName, "Stage image was rebuilt after the last push to the stage branch."),
        new(AliceName, "Watchtower picked it up, the container restarted by itself."),
        new(CarolName, "Reconnect works: the hub catches up with missed messages."),
    ];

    private static readonly DemoLine[] RandomLines =
    [
        new(CarolName, "Coffee or tea?"),
        new(DaveName, "Tea. Always tea."),
        new(BobName, "Coffee, and then more coffee."),
    ];

    private static readonly DemoLine[] DirectLines =
    [
        new(AliceName, "Hey Bob, did the stage deploy go through?"),
        new(BobName, "Yes, all containers are healthy."),
        new(AliceName, "Great, thanks!"),
    ];
}

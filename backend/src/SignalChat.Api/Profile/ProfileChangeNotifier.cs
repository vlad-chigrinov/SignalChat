using Microsoft.AspNetCore.SignalR;
using SignalChat.Api.Chat;
using SignalChat.Api.Hubs;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Profile;

/// <summary>
/// Tells the people who can see a user (room mates, accepted chats) and the user's own other tabs that the name or
/// the avatar changed, so no one needs a reload. Called by the REST endpoints after the change is stored.
/// </summary>
public sealed class ProfileChangeNotifier(IHubContext<ChatHub, IChatClient> hub, IPeerDirectory peers)
{
    public async Task NotifyAsync(User user, CancellationToken cancellationToken)
    {
        var audience = (await peers.GetPeerIdsAsync(user.Id, cancellationToken)).Append(user.Id);
        var groups = audience.Select(ChatGroups.User).ToList();
        await hub.Clients.Groups(groups).UserProfileChanged(
            ChatMapping.ToDto(new ChatUser(user.Id, user.DisplayName, user.AvatarUrl)));
    }
}

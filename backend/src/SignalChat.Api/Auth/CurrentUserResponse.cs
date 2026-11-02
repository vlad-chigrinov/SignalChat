using SignalChat.Domain.Users;

namespace SignalChat.Api.Auth;

/// <summary>Body of <c>GET /auth/me</c>. Email is returned to its owner only.</summary>
public sealed record CurrentUserResponse(Guid Id, string DisplayName, string? AvatarUrl, string Email)
{
    public static CurrentUserResponse From(User user) => new(user.Id, user.DisplayName, user.AvatarUrl, user.Email);
}

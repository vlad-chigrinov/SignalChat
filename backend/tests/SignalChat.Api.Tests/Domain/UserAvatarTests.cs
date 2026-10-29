using SignalChat.Domain.Users;

namespace SignalChat.Api.Tests.Domain;

public sealed class UserAvatarTests
{
    [Theory]
    [InlineData("https://lh3.googleusercontent.com/a/ACg8ocK=s96-c")]
    [InlineData("https://lh3.googleusercontent.com/a/photo.jpg")]
    public void GoogleHostedHttpsAvatar_IsKept(string avatarUrl)
    {
        var user = RegisterWithAvatar(avatarUrl);

        Assert.Equal(avatarUrl, user.AvatarUrl);
    }

    [Theory]
    [InlineData("http://lh3.googleusercontent.com/a/photo.jpg")]
    [InlineData("https://example.com/avatar.png")]
    [InlineData("https://evilgoogleusercontent.com/avatar.png")]
    [InlineData("https://googleusercontent.com/avatar.png")]
    [InlineData("https://lh3.googleusercontent.com.evil.example/avatar.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void AvatarOutsideGoogleHttps_IsDropped(string avatarUrl)
    {
        var user = RegisterWithAvatar(avatarUrl);

        Assert.Null(user.AvatarUrl);
    }

    [Fact]
    public void MissingAvatar_StaysNull()
    {
        var user = RegisterWithAvatar(null);

        Assert.Null(user.AvatarUrl);
    }

    private static User RegisterWithAvatar(string? avatarUrl) =>
        User.RegisterFromGoogle("avatar-sub", "avatar@example.com", "Avatar User", avatarUrl, DateTimeOffset.UtcNow);
}

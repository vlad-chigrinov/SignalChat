using SignalChat.Domain.Chat;
using SignalChat.Domain.Users;

namespace SignalChat.Api.Tests.Domain;

public sealed class UserProfileTests
{
    private const string GoogleAvatar = "https://lh3.googleusercontent.com/a/profile";

    [Fact]
    public void ChangeDisplayName_TrimsAndKeepsTheName()
    {
        var user = Register();

        user.ChangeDisplayName("  Новое имя  ");

        Assert.Equal("Новое имя", user.DisplayName);
    }

    [Fact]
    public void ChosenDisplayName_SurvivesTheNextGoogleSignIn()
    {
        var user = Register();
        user.ChangeDisplayName("My own name");

        user.RecordLogin("profile@example.com", "Google Name", GoogleAvatar, DateTimeOffset.UtcNow);

        Assert.Equal("My own name", user.DisplayName);
    }

    [Fact]
    public void UntouchedDisplayName_FollowsGoogle()
    {
        var user = Register();

        user.RecordLogin("profile@example.com", "Renamed At Google", GoogleAvatar, DateTimeOffset.UtcNow);

        Assert.Equal("Renamed At Google", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void EmptyDisplayName_IsRefused(string? name)
    {
        var user = Register();

        var error = Assert.Throws<ChatRuleException>(() => user.ChangeDisplayName(name));

        Assert.Equal(UserProfileErrorCodes.InvalidDisplayName, error.Code);
        Assert.Equal("Google Name", user.DisplayName);
    }

    [Fact]
    public void DisplayNameOverTheLimit_IsRefused()
    {
        var user = Register();

        var error = Assert.Throws<ChatRuleException>(
            () => user.ChangeDisplayName(new string('a', User.DisplayNameMaxLength + 1)));

        Assert.Equal(UserProfileErrorCodes.InvalidDisplayName, error.Code);
    }

    [Fact]
    public void DisplayNameAtTheLimit_IsKept()
    {
        var user = Register();
        var name = new string('a', User.DisplayNameMaxLength);

        user.ChangeDisplayName(name);

        Assert.Equal(name, user.DisplayName);
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("tab\there")]
    [InlineData("null\0byte")]
    [InlineData("escape\u001b[31m")]
    [InlineData("bidi\u202eoverride")]
    [InlineData("isolate\u2066x")]
    [InlineData("line\u2028separator")]
    public void ControlAndDirectionOverrideCharacters_AreRefused(string name)
    {
        var user = Register();

        var error = Assert.Throws<ChatRuleException>(() => user.ChangeDisplayName(name));

        Assert.Equal(UserProfileErrorCodes.InvalidDisplayName, error.Code);
    }

    [Fact]
    public void EmojiAndZeroWidthJoiner_AreAllowed()
    {
        var user = Register();

        user.ChangeDisplayName("Рыжий 🦊 👩‍💻");

        Assert.Equal("Рыжий 🦊 👩‍💻", user.DisplayName);
    }

    [Fact]
    public void UploadedAvatar_ReplacesGoogleAvatar_AndSurvivesTheNextSignIn()
    {
        var user = Register();

        user.UseUploadedAvatar();
        user.RecordLogin("profile@example.com", "Google Name", "https://lh3.googleusercontent.com/a/other", DateTimeOffset.UtcNow);

        Assert.True(user.HasCustomAvatar);
        Assert.Equal($"/api/users/{user.Id}/avatar?v=1", user.AvatarUrl);
    }

    [Fact]
    public void EachUpload_ChangesTheAvatarAddress_SoCachesLetGo()
    {
        var user = Register();

        user.UseUploadedAvatar();
        var first = user.AvatarUrl;
        user.UseUploadedAvatar();

        Assert.NotEqual(first, user.AvatarUrl);
        Assert.Equal($"/api/users/{user.Id}/avatar?v=2", user.AvatarUrl);
    }

    [Fact]
    public void UseGoogleAvatar_ReturnsToTheLatestGoogleAvatar()
    {
        var user = Register();
        user.UseUploadedAvatar();
        user.RecordLogin("profile@example.com", "Google Name", "https://lh3.googleusercontent.com/a/newer", DateTimeOffset.UtcNow);

        user.UseGoogleAvatar();

        Assert.False(user.HasCustomAvatar);
        Assert.Equal("https://lh3.googleusercontent.com/a/newer", user.AvatarUrl);
    }

    [Fact]
    public void UseGoogleAvatar_WithoutOne_LeavesNoAvatar()
    {
        var user = User.RegisterFromGoogle("no-avatar", "n@example.com", "N", null, DateTimeOffset.UtcNow);
        user.UseUploadedAvatar();

        user.UseGoogleAvatar();

        Assert.Null(user.AvatarUrl);
    }

    private static User Register() =>
        User.RegisterFromGoogle("profile-sub", "profile@example.com", "Google Name", GoogleAvatar, DateTimeOffset.UtcNow);
}

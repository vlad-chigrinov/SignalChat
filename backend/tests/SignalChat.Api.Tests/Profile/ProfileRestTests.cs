using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Auth;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Profile;

public sealed class ProfileRestTests(SignalChatApiFactory factory) : IClassFixture<SignalChatApiFactory>
{
    private const string GoogleAvatar = "https://lh3.googleusercontent.com/a/me";

    [Fact]
    public async Task Profile_WithoutSession_Returns401()
    {
        using var client = factory.CreateApiClient();

        var rename = await client.SendAsync(Put("/api/me/display-name", cookie: null, JsonContent.Create(new { displayName = "x" })));
        var upload = await client.SendAsync(Put("/api/me/avatar", cookie: null, PictureContent(TestImages.Png())));
        var reset = await client.SendAsync(ChatHttp.Delete("/api/me/avatar", cookie: null));

        Assert.Equal(HttpStatusCode.Unauthorized, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reset.StatusCode);
    }

    [Fact]
    public async Task ChangeDisplayName_StoresIt_AndEveryViewShowsIt()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("rename"));
        var (_, otherCookie) = await factory.CreateSessionAsync(Profile("rename-other"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Put("/api/me/display-name", cookie, JsonContent.Create(new { displayName = "  Brand New  " })));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Brand New", (await ChatHttp.ReadJsonAsync(response)).GetProperty("displayName").GetString());
        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        Assert.Equal("Brand New", me.GetProperty("displayName").GetString());
        var found = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/users/search?query=brand%20new", otherCookie)));
        Assert.Equal(user.Id, found[0].GetProperty("id").GetGuid());
        Assert.Equal("Brand New", found[0].GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public async Task ChangeDisplayName_Invalid_Returns400_WithCode_AndKeepsTheOldName(string name)
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("badname"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Put("/api/me/display-name", cookie, JsonContent.Create(new { displayName = name })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_display_name", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        Assert.Equal("User badname", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ChangeDisplayName_WithoutABody_Returns400()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("nobody"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Put("/api/me/display-name", cookie, JsonContent.Create(new { })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeDisplayName_FromAForeignOrigin_Returns403()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("csrf"));
        using var client = factory.CreateApiClient();
        var request = Put("/api/me/display-name", cookie, JsonContent.Create(new { displayName = "Hijacked" }));
        request.Headers.Remove("Origin");
        request.Headers.Add("Origin", SignalChatApiFactory.ForeignOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChosenName_SurvivesTheNextGoogleSignIn()
    {
        var google = Profile("relogin");
        var (_, cookie) = await factory.CreateSessionAsync(google);
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/display-name", cookie, JsonContent.Create(new { displayName = "Mine" })));

        await factory.CreateSessionAsync(google with { Name = "Google Changed It" });

        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        Assert.Equal("Mine", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task UploadAvatar_StoresAWebp_AndServesItAtTheAddressTheProfileReports()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("upload"));
        var (_, viewerCookie) = await factory.CreateSessionAsync(Profile("upload-viewer"));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Jpeg())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var avatarUrl = (await ChatHttp.ReadJsonAsync(response)).GetProperty("avatarUrl").GetString();
        Assert.Equal($"/api/users/{user.Id}/avatar?v=1", avatarUrl);
        var served = await client.SendAsync(ChatHttp.Get(avatarUrl!, viewerCookie));
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/webp", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        var cache = served.Headers.CacheControl!;
        Assert.True(cache.Private);
        Assert.True(cache.MaxAge > TimeSpan.FromDays(30));
        Assert.Contains("immutable", cache.ToString(), StringComparison.Ordinal);
        var bytes = await served.Content.ReadAsByteArrayAsync();
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
    }

    [Fact]
    public async Task Avatar_IsVisibleInChatViews()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("views"));
        var (_, otherCookie) = await factory.CreateSessionAsync(Profile("views-other"));
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Png())));

        var found = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/api/users/search?query=views", otherCookie)));

        var match = found.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == user.Id);
        Assert.Equal($"/api/users/{user.Id}/avatar?v=1", match.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task Avatar_WithoutSession_Returns401()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("private"));
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Png())));

        var response = await client.GetAsync($"/api/users/{user.Id}/avatar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_OfAUserWithoutOne_Returns404()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("none"));
        using var client = factory.CreateApiClient();

        var missing = await client.SendAsync(ChatHttp.Get($"/api/users/{user.Id}/avatar", cookie));
        var unknown = await client.SendAsync(ChatHttp.Get($"/api/users/{Guid.NewGuid()}/avatar", cookie));
        var malformed = await client.SendAsync(ChatHttp.Get("/api/users/not-a-guid/avatar", cookie));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_AgainChangesTheAddress_AndReplacesThePicture()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("again"));
        using var client = factory.CreateApiClient();
        var first = await ChatHttp.ReadJsonAsync(await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Png()))));

        var second = await ChatHttp.ReadJsonAsync(await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Jpeg()))));

        Assert.EndsWith("?v=1", first.GetProperty("avatarUrl").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("?v=2", second.GetProperty("avatarUrl").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("just text")]
    public async Task UploadAvatar_ThatIsNotAPicture_Returns400_WhateverItIsCalled(string content)
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("fake"));
        using var client = factory.CreateApiClient();
        var body = new StringContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var response = await client.SendAsync(Put("/api/me/avatar", cookie, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_avatar", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task UploadAvatar_RealPictureSentAsText_IsStillAccepted()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("mislabeled"));
        using var client = factory.CreateApiClient();
        var body = new ByteArrayContent(TestImages.Png());
        body.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var response = await client.SendAsync(Put("/api/me/avatar", cookie, body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_OverTheSizeLimit_Returns413_WithCode()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("huge"));
        using var client = factory.CreateApiClient();
        var body = new ByteArrayContent(new byte[AvatarLimits.MaxUploadBytes + 1]);

        var response = await client.SendAsync(Put("/api/me/avatar", cookie, body));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("avatar_too_large", (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task UploadAvatar_Refused_KeepsTheCurrentAvatar()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("keeps", GoogleAvatar));
        using var client = factory.CreateApiClient();

        await client.SendAsync(Put("/api/me/avatar", cookie, new ByteArrayContent([1, 2, 3])));

        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        Assert.Equal(GoogleAvatar, me.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task UploadedAvatar_SurvivesTheNextGoogleSignIn()
    {
        var google = Profile("keep-upload", GoogleAvatar);
        var (_, cookie) = await factory.CreateSessionAsync(google);
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Png())));

        await factory.CreateSessionAsync(google with { PictureUrl = "https://lh3.googleusercontent.com/a/changed" });

        var me = await ChatHttp.ReadJsonAsync(await client.SendAsync(ChatHttp.Get("/auth/me", cookie)));
        Assert.Contains("/avatar?v=1", me.GetProperty("avatarUrl").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetAvatar_ReturnsToTheGoogleOne_AndDeletesTheStoredPicture()
    {
        var (user, cookie) = await factory.CreateSessionAsync(Profile("reset", GoogleAvatar));
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/avatar", cookie, PictureContent(TestImages.Png())));

        var response = await client.SendAsync(ChatHttp.Delete("/api/me/avatar", cookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(GoogleAvatar, (await ChatHttp.ReadJsonAsync(response)).GetProperty("avatarUrl").GetString());
        var served = await client.SendAsync(ChatHttp.Get($"/api/users/{user.Id}/avatar", cookie));
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        Assert.False(await db.UserAvatars.AnyAsync(avatar => avatar.UserId == user.Id));
    }

    [Fact]
    public async Task ResetAvatar_WithNoUploadedOne_IsHarmless()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("noreset", GoogleAvatar));
        using var client = factory.CreateApiClient();

        var response = await client.SendAsync(ChatHttp.Delete("/api/me/avatar", cookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(GoogleAvatar, (await ChatHttp.ReadJsonAsync(response)).GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task ProfileResponses_DoNotRevealOtherUsersEmails()
    {
        var (_, cookie) = await factory.CreateSessionAsync(Profile("mail"));
        var (_, otherCookie) = await factory.CreateSessionAsync(Profile("mail-other"));
        using var client = factory.CreateApiClient();
        await client.SendAsync(Put("/api/me/display-name", cookie, JsonContent.Create(new { displayName = "Mailbox Person" })));

        var found = await client.SendAsync(ChatHttp.Get("/api/users/search?query=mailbox", otherCookie));

        Assert.DoesNotContain("@", await found.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static GoogleProfile Profile(string prefix, string? pictureUrl = null) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", pictureUrl);

    private static ByteArrayContent PictureContent(byte[] picture)
    {
        var content = new ByteArrayContent(picture);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static HttpRequestMessage Put(string path, string? cookie, HttpContent body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = body };
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        request.Headers.Add("Origin", SignalChatApiFactory.AllowedOrigin);
        return request;
    }
}

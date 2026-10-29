using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SignalChat.Api.Auth;
using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Hubs;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>The test-only sign-in: same session as Google, handle validation, two users in one hub.</summary>
public sealed class TestLoginTests(E2eTestAuthApiFactory factory) : IClassFixture<E2eTestAuthApiFactory>
{
    [Fact]
    public async Task Login_CreatesTheUserAndASessionThatWorksLikeAGoogleOne()
    {
        using var client = factory.CreateApiClient();

        var cookie = await LoginAsync(client, "alice");
        var me = await client.SendAsync(ChatHttp.Get("/auth/me", cookie));

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await ChatHttp.ReadJsonAsync(me);
        Assert.Equal("alice", profile.GetProperty("displayName").GetString());
        Assert.Equal("alice@e2e.invalid", profile.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Login_WithTheSameHandleAgain_IsTheSameUser()
    {
        using var client = factory.CreateApiClient();

        var first = await UserIdAsync(client, await LoginAsync(client, "repeat-user"));
        var second = await UserIdAsync(client, await LoginAsync(client, "repeat-user"));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task TwoTestUsers_ShareARoomOverTheHub()
    {
        using var client = factory.CreateApiClient();
        var aliceCookie = await LoginAsync(client, "hub-alice");
        var bobCookie = await LoginAsync(client, "hub-bob");
        var roomId = await HubFixtures.CreateRoomAsync(client, aliceCookie);
        await HubFixtures.JoinRoomAsync(client, bobCookie, roomId);
        await using var alice = await HubTestClient.ConnectAsync(factory, aliceCookie);
        await using var bob = await HubTestClient.ConnectAsync(factory, bobCookie);
        await alice.InvokeAsync("JoinRoom", roomId.ToString());
        await bob.InvokeAsync("JoinRoom", roomId.ToString());
        var aliceInbox = new HubEventInbox<MessageDto>(alice, "MessageReceived");

        await bob.InvokeAsync<MessageDto>("SendMessage", roomId.ToString(), "hi from bob");

        Assert.Equal("hi from bob", (await aliceInbox.NextAsync()).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Alice")]
    [InlineData("a b")]
    [InlineData("a.b")]
    [InlineData("привет")]
    public async Task InvalidHandle_IsBadRequest(string handle)
    {
        using var client = factory.CreateApiClient();

        var response = await PostTestLoginAsync(client, new { handle });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleLongerThanTheLimit_IsBadRequest()
    {
        using var client = factory.CreateApiClient();

        var response = await PostTestLoginAsync(client, new { handle = new string('a', TestHandle.MaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingBody_IsBadRequest()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync("/auth/test-login", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string handle)
    {
        var response = await PostTestLoginAsync(client, new { handle });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return SetCookies.Find(response.Headers.GetValues("Set-Cookie"), AuthSchemes.SessionCookieName)!.NameValue();
    }

    private static Task<HttpResponseMessage> PostTestLoginAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/auth/test-login", body);

    private static async Task<Guid> UserIdAsync(HttpClient client, string cookie)
    {
        var me = await client.SendAsync(ChatHttp.Get("/auth/me", cookie));
        return (await ChatHttp.ReadJsonAsync(me)).GetProperty("id").GetGuid();
    }
}

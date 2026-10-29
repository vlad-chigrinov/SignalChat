using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>Shared setup for hub tests: rooms and memberships over REST, and message lookups in the database.</summary>
internal static class HubFixtures
{
    public static async Task<Guid> CreateRoomAsync(HttpClient client, string cookie)
    {
        var response = await client.SendAsync(
            ChatHttp.Post("/api/rooms", cookie, new { name = ChatHttp.UniqueName("Hub"), description = "" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ChatHttp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task JoinRoomAsync(HttpClient client, string cookie, Guid roomId)
    {
        var response = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/join", cookie));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task LeaveRoomOverRestAsync(HttpClient client, string cookie, Guid roomId)
    {
        var response = await client.SendAsync(ChatHttp.Post($"/api/rooms/{roomId}/leave", cookie));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<int> StoredCountAsync(SignalChatApiFactory factory, string text)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        return await db.Messages.CountAsync(message => message.Text == text);
    }

    public static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}@example.com", $"User {prefix}", null);
}

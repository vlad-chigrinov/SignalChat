using System.Net;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Consent-flow REST steps shared by the REST and hub tests.</summary>
internal static class DirectRequestFixtures
{
    /// <summary>Sends a request from the caller to the peer and returns the new conversation id.</summary>
    public static async Task<Guid> RequestAsync(HttpClient client, string cookie, Guid peerId, string text)
    {
        var response = await client.SendAsync(ChatHttp.Post("/api/direct-conversations", cookie, new { userId = peerId, text }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ChatHttp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task AcceptAsync(HttpClient client, string cookie, Guid conversationId)
    {
        var response = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{conversationId}/accept", cookie));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task DeclineAsync(HttpClient client, string cookie, Guid conversationId)
    {
        var response = await client.SendAsync(ChatHttp.Post($"/api/direct-conversations/{conversationId}/decline", cookie));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

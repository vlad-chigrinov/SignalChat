using System.Net.Http.Json;
using System.Text.Json;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Builds authenticated requests the way the SPA sends them (cookie plus allowed Origin).</summary>
internal static class ChatHttp
{
    public static HttpRequestMessage Get(string path, string cookie) => Build(HttpMethod.Get, path, cookie, body: null);

    public static HttpRequestMessage Post(string path, string cookie, object? body = null) =>
        Build(HttpMethod.Post, path, cookie, body);

    public static HttpRequestMessage Put(string path, string cookie, object? body = null) =>
        Build(HttpMethod.Put, path, cookie, body);

    /// <summary>A DELETE as the SPA sends it; a null cookie means a visitor without a session.</summary>
    public static HttpRequestMessage Delete(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        request.Headers.Add("Origin", SignalChatApiFactory.AllowedOrigin);
        return request;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static HttpRequestMessage Build(HttpMethod method, string path, string cookie, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", SignalChatApiFactory.AllowedOrigin);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }
}

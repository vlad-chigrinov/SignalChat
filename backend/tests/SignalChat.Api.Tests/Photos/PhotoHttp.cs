using System.Net;
using System.Net.Http.Headers;
using SignalChat.Api.Tests.Chat;

namespace SignalChat.Api.Tests.Photos;

/// <summary>One file of a multipart upload as a test describes it.</summary>
internal sealed record UploadFile(string FileName, string ContentType, byte[] Content)
{
    public static UploadFile Png(int width = 120, int height = 80, string name = "picture.png") =>
        new(name, "image/png", Profile.TestImages.Png(width, height));
}

/// <summary>Builds the multipart request the SPA sends for a photo message, and reads the answers.</summary>
internal static class PhotoHttp
{
    public static string SendPath(Guid conversationId) => $"/api/conversations/{conversationId}/messages/photos";

    public static HttpRequestMessage Send(Guid conversationId, string? cookie, string? text, params UploadFile[] files)
    {
        var form = new MultipartFormDataContent();
        if (text is not null)
        {
            form.Add(new StringContent(text), "text");
        }
        foreach (var file in files)
        {
            var part = new ByteArrayContent(file.Content);
            part.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            form.Add(part, "files", file.FileName);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, SendPath(conversationId)) { Content = form };
        request.Headers.Add("Origin", Infrastructure.SignalChatApiFactory.AllowedOrigin);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        return request;
    }

    public static async Task<System.Text.Json.JsonElement> SendOkAsync(
        HttpClient client, Guid conversationId, string cookie, string? text, params UploadFile[] files)
    {
        var response = await client.SendAsync(Send(conversationId, cookie, text, files));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ChatHttp.ReadJsonAsync(response);
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await ChatHttp.ReadJsonAsync(response)).GetProperty("code").GetString();
}

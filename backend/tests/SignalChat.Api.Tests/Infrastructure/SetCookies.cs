using Microsoft.Net.Http.Headers;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>Helpers to read Set-Cookie headers and replay them as a Cookie header.</summary>
public static class SetCookies
{
    public static SetCookieHeaderValue? Find(IEnumerable<string?> setCookieHeaders, string name) =>
        Parse(setCookieHeaders).FirstOrDefault(cookie => cookie.Name.Equals(name, StringComparison.Ordinal));

    public static IReadOnlyList<SetCookieHeaderValue> Parse(IEnumerable<string?> setCookieHeaders) =>
        SetCookieHeaderValue.ParseList(setCookieHeaders.OfType<string>().ToList()).ToList();

    public static IReadOnlyList<SetCookieHeaderValue> From(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? Parse(values) : [];

    /// <summary>"name=value; name2=value2" for every cookie the response set (expired ones excluded).</summary>
    public static string ToCookieHeader(HttpResponseMessage response) =>
        string.Join("; ", From(response)
            .Where(cookie => cookie.Expires is null || cookie.Expires > DateTimeOffset.UtcNow)
            .Select(cookie => cookie.NameValue()));

    public static string NameValue(this SetCookieHeaderValue cookie) => $"{cookie.Name}={cookie.Value}";
}

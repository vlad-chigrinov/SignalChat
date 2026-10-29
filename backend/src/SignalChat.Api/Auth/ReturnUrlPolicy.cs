using SignalChat.Api.Configuration;

namespace SignalChat.Api.Auth;

/// <summary>Builds the post-login destination. Only local SPA paths are honoured: no open redirect.</summary>
public static class ReturnUrlPolicy
{
    private const string DefaultPath = "/";

    public static string ToFrontendUrl(FrontendSettings frontend, string? returnUrl)
    {
        var path = IsLocalPath(returnUrl) ? returnUrl! : DefaultPath;
        return new Uri(frontend.BaseUrl, path).ToString();
    }

    /// <summary>"/rooms?x=1" yes; "//evil.com", "/\evil.com", "https://evil.com" no.</summary>
    private static bool IsLocalPath(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'))
        && !value.Any(char.IsControl);
}

namespace SignalChat.Api.Configuration;

/// <summary>
/// Where the SPA lives and which browser origins may call the API with the session cookie.
/// Read from <c>FRONTEND_URL</c> and <c>CORS_ALLOWED_ORIGINS</c> (comma-separated); no wildcard.
/// </summary>
public sealed class FrontendSettings
{
    public const string BaseUrlKey = "FRONTEND_URL";
    public const string AllowedOriginsKey = "CORS_ALLOWED_ORIGINS";
    private const string Wildcard = "*";

    private readonly HashSet<string> _allowedOrigins;

    private FrontendSettings(Uri baseUrl, IEnumerable<string> allowedOrigins)
    {
        BaseUrl = baseUrl;
        _allowedOrigins = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Absolute URL of the SPA root; the browser lands here after sign-in.</summary>
    public Uri BaseUrl { get; }

    public IReadOnlyCollection<string> AllowedOrigins => _allowedOrigins;

    public bool IsAllowedOrigin(string origin) => _allowedOrigins.Contains(origin);

    public static FrontendSettings FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = ParseAbsolute(configuration[BaseUrlKey], BaseUrlKey);
        var origins = (configuration[AllowedOriginsKey] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin == Wildcard
                ? throw new InvalidOperationException($"{AllowedOriginsKey} must list explicit origins, not '*'.")
                : ToOrigin(ParseAbsolute(origin, AllowedOriginsKey)))
            .ToList();

        if (origins.Count == 0)
        {
            throw new InvalidOperationException($"{AllowedOriginsKey} must list at least one origin.");
        }
        return new FrontendSettings(baseUrl, origins);
    }

    private static Uri ParseAbsolute(string? value, string key) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : throw new InvalidOperationException($"{key} must be an absolute http(s) URL, got '{value}'.");

    /// <summary>Origin as browsers send it: scheme://host[:port], no trailing slash.</summary>
    private static string ToOrigin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);
}

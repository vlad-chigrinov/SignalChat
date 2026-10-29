namespace SignalChat.Api.Auth;

/// <summary>Google OAuth client credentials, from <c>GOOGLE_CLIENT_ID</c> / <c>GOOGLE_CLIENT_SECRET</c> in .env.</summary>
public sealed record GoogleSettings(string? ClientId, string? ClientSecret)
{
    public const string ClientIdKey = "GOOGLE_CLIENT_ID";
    public const string ClientSecretKey = "GOOGLE_CLIENT_SECRET";

    /// <summary>Without credentials the API still runs; only <c>/auth/login</c> answers 503.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public static GoogleSettings FromConfiguration(IConfiguration configuration) =>
        new(configuration[ClientIdKey], configuration[ClientSecretKey]);
}

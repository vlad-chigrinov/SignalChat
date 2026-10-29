using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>
/// Stands in for accounts.google.com: static discovery document, an RSA signing key,
/// and a token endpoint that answers with an ID token for whatever <see cref="NextUser"/> says.
/// </summary>
public sealed class FakeGoogle
{
    public const string Issuer = "https://accounts.google.com";
    public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string ClientId = "test-client-id.apps.googleusercontent.com";
    public const string ClientSecret = "test-client-secret";
    private const string FakeAccessToken = "fake-google-access-token";

    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "fake-google-key" };

    public FakeGoogle()
    {
        Configuration = new OpenIdConnectConfiguration
        {
            Issuer = Issuer,
            AuthorizationEndpoint = AuthorizationEndpoint,
            TokenEndpoint = TokenEndpoint,
        };
        Configuration.SigningKeys.Add(_signingKey);
        TokenEndpointHandler = new TokenHandler(this);
    }

    public OpenIdConnectConfiguration Configuration { get; }

    public HttpMessageHandler TokenEndpointHandler { get; }

    /// <summary>Profile put in the next ID token. Nonce comes from the authorize redirect.</summary>
    public FakeGoogleUser NextUser { get; set; } = new("google-sub-1", "alice@example.com", "Alice", true);

    public string? ExpectedNonce { get; set; }

    /// <summary>Form body of the last token request (to assert PKCE <c>code_verifier</c>).</summary>
    public IReadOnlyDictionary<string, string> LastTokenRequest { get; private set; } = new Dictionary<string, string>();

    private string CreateIdToken()
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = NextUser.Subject,
                ["email"] = NextUser.Email,
                ["email_verified"] = NextUser.EmailVerified,
                ["name"] = NextUser.Name,
                ["picture"] = "https://lh3.googleusercontent.com/a/avatar",
                ["nonce"] = ExpectedNonce ?? string.Empty,
                ["at_hash"] = AccessTokenHash(FakeAccessToken),
            },
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string AccessTokenHash(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));
        return Base64UrlEncoder.Encode(hash.AsSpan(0, hash.Length / 2).ToArray());
    }

    private sealed class TokenHandler(FakeGoogle google) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.ToString() != TokenEndpoint)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            google.LastTokenRequest = form.Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["access_token"] = FakeAccessToken,
                ["id_token"] = google.CreateIdToken(),
                ["token_type"] = "Bearer",
                ["expires_in"] = 3600,
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}

public sealed record FakeGoogleUser(string Subject, string Email, string Name, bool EmailVerified);

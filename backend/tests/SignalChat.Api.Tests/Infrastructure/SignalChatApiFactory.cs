using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SignalChat.Api.Auth;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory with SQLite instead of PostgreSQL and a fake Google, so the whole
/// auth flow is exercised without network access or real credentials.
/// </summary>
public class SignalChatApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "http://localhost:5173";
    public const string ForeignOrigin = "https://evil.example";
    public static readonly Uri ApiBaseAddress = new("https://localhost");

    // Base64 of 32 fixed bytes: deterministic, test-only.
    private const string TestPersonalDataKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private const string UnusedPostgres = "Host=localhost;Database=unused";

    // A named shared-cache in-memory database: every DbContext opens its own connection (the hub and REST run
    // concurrently), and the sentinel connection keeps the database alive for the lifetime of this host.
    private readonly string _databaseConnectionString =
        $"Data Source=signalchat-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _sentinel;

    public SignalChatApiFactory()
    {
        _sentinel = new SqliteConnection(_databaseConnectionString);
        _sentinel.Open();
    }

    public FakeGoogle Google { get; } = new();

    /// <summary>The API's clock: tests move it to exercise the absolute session lifetime.</summary>
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected virtual bool GoogleConfigured => true;

    protected virtual string EnvironmentName => "Testing";

    protected virtual bool E2eTestAuthEnabled => false;

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = ApiBaseAddress,
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    /// <summary>Signs a user in exactly as the Google callback would and returns the session cookie ("name=value").</summary>
    public async Task<(User User, string Cookie)> CreateSessionAsync(GoogleProfile? profile = null)
    {
        using var scope = Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var user = await accounts.SignInWithGoogleAsync(
            profile ?? new GoogleProfile("session-sub", "session@example.com", "Session User", null), CancellationToken.None);

        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Scheme = ApiBaseAddress.Scheme;
        http.Request.Host = new HostString(ApiBaseAddress.Host);
        await http.SignInAsync(
            AuthSchemes.Session, SessionPrincipal.Create(user, Clock.GetUtcNow()), new AuthenticationProperties { IsPersistent = true });

        return (user, SetCookies.Find(http.Response.Headers.SetCookie, AuthSchemes.SessionCookieName)!.NameValue());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting(E2eTestAuthSettings.FlagKey, E2eTestAuthEnabled ? "true" : "false");
        builder.UseSetting("ConnectionStrings:Default", UnusedPostgres);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("PII_ENCRYPTION_KEY", TestPersonalDataKey);
        builder.UseSetting("FRONTEND_URL", AllowedOrigin);
        builder.UseSetting("CORS_ALLOWED_ORIGINS", AllowedOrigin);
        builder.UseSetting(GoogleSettings.ClientIdKey, GoogleConfigured ? FakeGoogle.ClientId : string.Empty);
        builder.UseSetting(GoogleSettings.ClientSecretKey, GoogleConfigured ? FakeGoogle.ClientSecret : string.Empty);

        builder.ConfigureTestServices(services =>
        {
            ReplaceDatabaseWithSqlite(services);
            services.AddSingleton<TimeProvider>(Clock);
            services.PostConfigure<OpenIdConnectOptions>(AuthSchemes.Google, options =>
            {
                options.Configuration = Google.Configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(Google.Configuration);
                options.Backchannel = new HttpClient(Google.TokenEndpointHandler);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SignalChatDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _sentinel.Dispose();
        }
    }

    private void ReplaceDatabaseWithSqlite(IServiceCollection services)
    {
        var npgsqlRegistrations = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions<SignalChatDbContext>)
                || descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<SignalChatDbContext>))
            .ToList();
        foreach (var descriptor in npgsqlRegistrations)
        {
            services.Remove(descriptor);
        }
        services.AddDbContext<SignalChatDbContext>(options => options
            .UseSqlite(_databaseConnectionString)
            .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetModelCustomizer>());
    }
}

/// <summary>Same API, but GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET left empty.</summary>
public sealed class GoogleNotConfiguredApiFactory : SignalChatApiFactory
{
    protected override bool GoogleConfigured => false;
}

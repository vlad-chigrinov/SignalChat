using SignalChat.Api.Auth;
using SignalChat.Api.Chat;
using SignalChat.Api.Configuration;
using SignalChat.Api.Health;
using SignalChat.Api.Hubs;
using SignalChat.Api.Security;
using SignalChat.Infrastructure;
using SignalChat.Infrastructure.Persistence;

DotEnvLoader.LoadInDevelopment(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

var e2eTestAuth = E2eTestAuthSettings.FromConfiguration(builder.Configuration);
e2eTestAuth.EnsureAllowedIn(builder.Environment.EnvironmentName);

var frontend = FrontendSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(frontend);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSessionAuthentication(builder.Configuration);
builder.Services.AddFrontendCors(frontend);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ChatRuleExceptionHandler>();
builder.Services.AddChatSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseMiddleware<OriginGuardMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
if (e2eTestAuth.Enabled)
{
    app.MapTestLoginEndpoints();
}
app.MapHub<ChatHub>(ChatHub.Route);
app.MapChatEndpoints();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;

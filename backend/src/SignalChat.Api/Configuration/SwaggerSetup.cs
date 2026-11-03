using Microsoft.OpenApi;
using SignalChat.Api.Auth;
using SignalChat.Api.Hubs;

namespace SignalChat.Api.Configuration;

/// <summary>
/// Swagger (OpenAPI description and UI) for the stage environment, with the SignalR hub described by SignalRSwaggerGen. Mapped only in Development, so it is
/// never part of a production host. Calls from the UI use the session cookie: sign in with the Google link in the description first (<c>/auth/swagger-login</c>).
/// </summary>
public static class SwaggerSetup
{
    public const string DocumentName = "v1";
    public const string RoutePrefix = "swagger";

    private const string Title = "SignalChat API (stage)";

    private const string Description =
        $"Requests use the session cookie. [Sign in with Google]({SwaggerLoginEndpoint.Path}) first, " +
        "then check it with `GET /auth/me`. SignalR hub methods are documentation only: connect with a SignalR client to call them.";

    public static IServiceCollection AddStageSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo { Title = Title, Version = DocumentName, Description = Description });
            options.AddSignalRSwaggerGen(signalR => signalR.ScanAssembly(typeof(ChatHub).Assembly));
        });
        return services;
    }

    public static WebApplication UseStageSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = RoutePrefix;
            options.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", Title);
        });
        return app;
    }
}

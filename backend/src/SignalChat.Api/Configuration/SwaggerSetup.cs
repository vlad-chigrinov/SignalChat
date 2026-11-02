using Microsoft.OpenApi;

namespace SignalChat.Api.Configuration;

/// <summary>
/// Swagger (OpenAPI description and UI) for the stage environment. Mapped only in Development, so it is
/// never part of a production host. Calls from the UI use the session cookie: sign in through <c>/auth/login</c> first.
/// </summary>
public static class SwaggerSetup
{
    public const string DocumentName = "v1";
    public const string RoutePrefix = "swagger";

    private const string Title = "SignalChat API (stage)";

    public static IServiceCollection AddStageSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
            options.SwaggerDoc(DocumentName, new OpenApiInfo { Title = Title, Version = DocumentName }));
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

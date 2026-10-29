using SignalChat.Api.Configuration;

namespace SignalChat.Api.Security;

public static class CorsSetup
{
    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    /// <summary>Explicit origin allowlist with credentials; never <c>AllowAnyOrigin</c>.</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, FrontendSettings frontend)
    {
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins([.. frontend.AllowedOrigins])
            .AllowCredentials()
            .AllowAnyHeader()
            .WithMethods(AllowedMethods)));
        return services;
    }
}

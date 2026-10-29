using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Security;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Tag of health checks that need external dependencies (readiness, not liveness).</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = PostgresConnectionString.Resolve(configuration);
        var personalDataKey = PersonalDataKey.FromConfiguration(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPersonalDataCipher>(new AesGcmPersonalDataCipher(personalDataKey));
        services.AddDbContext<SignalChatDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserAccountService, UserAccountService>();

        services.AddHealthChecks()
            .AddDbContextCheck<SignalChatDbContext>("database", tags: [ReadinessTag]);

        return services;
    }
}

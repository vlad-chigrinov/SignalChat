using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Infrastructure.Chat;
using SignalChat.Infrastructure.Persistence;
using SignalChat.Infrastructure.Security;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Tag of health checks that need external dependencies (readiness, not liveness).</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool requireTlsByDefault = false)
    {
        var connectionString = PostgresConnectionString.Resolve(configuration, requireTlsByDefault);
        var personalDataKey = PersonalDataKey.FromConfiguration(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPersonalDataCipher>(new AesGcmPersonalDataCipher(personalDataKey));
        services.AddDbContext<SignalChatDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddSingleton<IAvatarImageProcessor, AvatarImageProcessor>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddChatServices();

        services.AddHealthChecks()
            .AddDbContextCheck<SignalChatDbContext>("database", tags: [ReadinessTag]);

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace SignalChat.Infrastructure.Persistence.Seed;

/// <summary>Runs <see cref="DemoDataSeeder"/> once the schema is in place; used at startup of the stage environment.</summary>
public static class DemoDataSeedRunner
{
    public static async Task SeedDemoDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = new DemoDataSeeder(
            scope.ServiceProvider.GetRequiredService<SignalChatDbContext>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>());
        await seeder.SeedAsync(cancellationToken);
    }
}

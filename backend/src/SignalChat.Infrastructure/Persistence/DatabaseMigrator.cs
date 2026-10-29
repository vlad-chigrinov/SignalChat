using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SignalChat.Infrastructure.Persistence;

/// <summary>Applies pending EF Core migrations; used at startup in local development.</summary>
public static class DatabaseMigrator
{
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

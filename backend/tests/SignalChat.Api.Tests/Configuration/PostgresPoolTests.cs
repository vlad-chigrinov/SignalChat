using Microsoft.Extensions.Configuration;
using Npgsql;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>
/// One PostgreSQL server (max_connections=30, deploy/docker-compose.infra.yml) serves both environments. The pools of
/// all of them together must stay well below it, or a busy API takes every slot and migrations or maintenance cannot connect.
/// </summary>
public sealed class PostgresPoolTests
{
    /// <summary>max_connections of the shared database container.</summary>
    private const int SharedMaxConnections = 30;

    /// <summary>main and stage, each with its own API and its own pool.</summary>
    private const int EnvironmentsOnTheServer = 2;

    /// <summary>Slots kept free for superuser, migrations and manual maintenance.</summary>
    private const int ReservedConnections = 5;

    [Fact]
    public void BuiltConnectionString_CapsThePoolBelowTheDatabaseLimit()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["POSTGRES_HOST"] = "db",
            ["POSTGRES_DB"] = "signalchat",
            ["POSTGRES_USER"] = "signalchat",
            ["POSTGRES_PASSWORD"] = "test-only-password",
        }).Build();

        var builder = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Resolve(configuration));

        Assert.True(builder.MaxPoolSize * EnvironmentsOnTheServer <= SharedMaxConnections - ReservedConnections);
    }
}

using Microsoft.Extensions.Configuration;
using Npgsql;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>
/// The production PostgreSQL runs with max_connections=20 (docker-compose.prod.yml). The Npgsql pool
/// must stay well below it, or a busy API takes every slot and migrations or maintenance cannot connect.
/// </summary>
public sealed class PostgresPoolTests
{
    /// <summary>max_connections of the production database container.</summary>
    private const int ProductionMaxConnections = 20;

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

        Assert.True(builder.MaxPoolSize <= ProductionMaxConnections - ReservedConnections);
    }
}

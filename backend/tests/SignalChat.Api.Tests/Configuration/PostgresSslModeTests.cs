using Microsoft.Extensions.Configuration;
using Npgsql;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Configuration;

/// <summary>TLS to PostgreSQL: Production requires it by default, the internal Docker network turns it off explicitly, and a typo stops the host.</summary>
public sealed class PostgresSslModeTests
{
    private const string SslModeKey = "POSTGRES_SSL_MODE";

    [Fact]
    public void Production_RequiresTlsWhenNothingIsConfigured()
    {
        var builder = Resolve(new Dictionary<string, string?>(), requireTlsByDefault: true);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void OutsideProduction_KeepsNpgsqlDefault()
    {
        var builder = Resolve(new Dictionary<string, string?>(), requireTlsByDefault: false);

        Assert.Equal(SslMode.Prefer, builder.SslMode);
    }

    [Fact]
    public void ExplicitMode_WinsOverTheProductionDefault()
    {
        var builder = Resolve(new Dictionary<string, string?> { [SslModeKey] = "Disable" }, requireTlsByDefault: true);

        Assert.Equal(SslMode.Disable, builder.SslMode);
    }

    [Fact]
    public void UnknownMode_StopsTheHostWithTheSettingName()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Resolve(new Dictionary<string, string?> { [SslModeKey] = "Sometimes" }, requireTlsByDefault: true));

        Assert.Contains(SslModeKey, error.Message);
    }

    private static NpgsqlConnectionStringBuilder Resolve(Dictionary<string, string?> extra, bool requireTlsByDefault)
    {
        var values = new Dictionary<string, string?>
        {
            ["POSTGRES_HOST"] = "db",
            ["POSTGRES_DB"] = "signalchat",
            ["POSTGRES_USER"] = "signalchat",
            ["POSTGRES_PASSWORD"] = "test-only-password",
        };
        foreach (var pair in extra)
        {
            values[pair.Key] = pair.Value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new NpgsqlConnectionStringBuilder(PostgresConnectionString.Resolve(configuration, requireTlsByDefault));
    }
}

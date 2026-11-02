using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SignalChat.Infrastructure.Persistence;

/// <summary>
/// Resolves the PostgreSQL connection string. An explicit <c>ConnectionStrings:Default</c> wins;
/// otherwise it is built from the same <c>POSTGRES_*</c> variables docker-compose uses,
/// so the password is declared once, in <c>.env</c>.
/// </summary>
public static class PostgresConnectionString
{
    private const string ExplicitKey = "Default";
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5432;
    private const string SslModeKey = "POSTGRES_SSL_MODE";

    /// <summary>
    /// Upper bound of the Npgsql pool. One database server allows 30 connections in total and serves both the main and
    /// the stage environment (deploy/docker-compose.infra.yml), so each pool leaves room for the other and for maintenance.
    /// </summary>
    private const int MaxPoolSize = 10;

    /// <param name="requireTlsByDefault">
    /// True in Production: TLS is required unless <c>POSTGRES_SSL_MODE</c> says otherwise. The internal Docker
    /// network sets it to Disable explicitly; an external database keeps Require.
    /// </param>
    public static string Resolve(IConfiguration configuration, bool requireTlsByDefault = false)
    {
        var explicitValue = configuration.GetConnectionString(ExplicitKey);
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = configuration["POSTGRES_HOST"] ?? DefaultHost,
            Port = int.TryParse(configuration["POSTGRES_PORT"], out var port) ? port : DefaultPort,
            Database = Require(configuration, "POSTGRES_DB"),
            Username = Require(configuration, "POSTGRES_USER"),
            Password = Require(configuration, "POSTGRES_PASSWORD"),
            MaxPoolSize = MaxPoolSize,
        };
        ApplySslMode(builder, configuration, requireTlsByDefault);
        return builder.ConnectionString;
    }

    private static void ApplySslMode(NpgsqlConnectionStringBuilder builder, IConfiguration configuration, bool requireTlsByDefault)
    {
        var configured = configuration[SslModeKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (requireTlsByDefault)
            {
                builder.SslMode = SslMode.Require;
            }
            return;
        }

        builder.SslMode = Enum.TryParse<SslMode>(configured, ignoreCase: true, out var mode)
            ? mode
            : throw new InvalidOperationException(
                $"{SslModeKey} must be one of Disable, Allow, Prefer, Require, VerifyCA or VerifyFull, got '{configured}'.");
    }

    private static string Require(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Database setting '{key}' is missing. Copy .env.example to .env at the repository root.");
}

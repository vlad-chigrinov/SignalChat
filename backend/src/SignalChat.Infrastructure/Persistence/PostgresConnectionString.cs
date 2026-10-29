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

    public static string Resolve(IConfiguration configuration)
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
        };
        return builder.ConnectionString;
    }

    private static string Require(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Database setting '{key}' is missing. Copy .env.example to .env at the repository root.");
}

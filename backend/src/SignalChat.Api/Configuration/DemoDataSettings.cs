namespace SignalChat.Api.Configuration;

/// <summary>
/// Demo data for the stage environment: <c>SEED_DEMO_DATA=true</c> fills an empty database with demo people, rooms and
/// messages at startup. Off by default. Allowed only in Development (how stage runs): switching it on anywhere else
/// stops the application at startup, so demo people can never appear in production.
/// </summary>
public sealed class DemoDataSettings
{
    public const string FlagKey = "SEED_DEMO_DATA";

    private const string AllowedEnvironment = "Development";

    private DemoDataSettings(bool enabled)
    {
        Enabled = enabled;
    }

    public bool Enabled { get; }

    public static DemoDataSettings FromConfiguration(IConfiguration configuration) =>
        new(configuration.GetValue<bool>(FlagKey));

    /// <summary>Throws when the flag is on in an environment other than <see cref="AllowedEnvironment"/>.</summary>
    public void EnsureAllowedIn(string environmentName)
    {
        if (!Enabled || string.Equals(environmentName, AllowedEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{FlagKey}=true is refused in the '{environmentName}' environment: demo data is allowed only in "
            + $"{AllowedEnvironment}. Remove {FlagKey} from the environment.");
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SignalChat.Api.Auth;

/// <summary>
/// Test-only sign-in for the end-to-end runs: <c>POST /auth/test-login</c> creates a session without Google.
/// Off by default. Allowed only in Testing and E2E: switching it on anywhere else (Development included) stops the application at startup.
/// </summary>
public sealed class E2eTestAuthSettings
{
    public const string FlagKey = "E2E_TEST_AUTH";

    private static readonly string[] AllowedEnvironments = ["Testing", "E2E"];

    private E2eTestAuthSettings(bool enabled)
    {
        Enabled = enabled;
    }

    public bool Enabled { get; }

    public static E2eTestAuthSettings FromConfiguration(IConfiguration configuration) =>
        new(configuration.GetValue<bool>(FlagKey));

    /// <summary>Records in the log that the test sign-in is on, so a production log shows it at once.</summary>
    public void WarnIfEnabled(ILogger logger)
    {
        if (Enabled)
        {
            logger.LogWarning(
                "{FlagKey} is on: POST /auth/test-login creates sessions without Google. Use it only for end-to-end runs.",
                FlagKey);
        }
    }

    /// <summary>Throws when the flag is on in an environment that is not one of <see cref="AllowedEnvironments"/>.</summary>
    public void EnsureAllowedIn(string environmentName)
    {
        if (!Enabled || AllowedEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{FlagKey}=true is refused in the '{environmentName}' environment: test sign-in is allowed only in "
            + $"{string.Join(", ", AllowedEnvironments)}. Remove {FlagKey} from the environment.");
    }
}

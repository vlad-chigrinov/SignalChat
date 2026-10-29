namespace SignalChat.Api.Configuration;

/// <summary>
/// Loads <c>KEY=VALUE</c> pairs from the repository-root <c>.env</c> into process environment
/// variables, so <c>dotnet run</c> and docker-compose share one secrets file.
/// Variables already set in the environment win; a missing file is not an error.
/// Development only: tests and deployed environments never read a developer's local secrets.
/// </summary>
public static class DotEnvLoader
{
    private const string FileName = ".env";
    private const char CommentPrefix = '#';
    private const char Separator = '=';

    public static void LoadInDevelopment(string startDirectory)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = FindUpwards(startDirectory);
        if (path is null)
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (TryParse(line, out var key, out var value) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindUpwards(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static bool TryParse(string line, out string key, out string value)
    {
        key = value = string.Empty;
        var trimmed = line.Trim();
        var separatorIndex = trimmed.IndexOf(Separator);
        if (trimmed.Length == 0 || trimmed[0] == CommentPrefix || separatorIndex <= 0)
        {
            return false;
        }

        key = trimmed[..separatorIndex].Trim();
        value = trimmed[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        return true;
    }
}

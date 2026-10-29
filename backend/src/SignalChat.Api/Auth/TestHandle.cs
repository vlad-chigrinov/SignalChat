namespace SignalChat.Api.Auth;

/// <summary>The test user name: 1..32 characters of a-z, 0-9, underscore and hyphen.</summary>
public static class TestHandle
{
    public const int MaxLength = 32;

    /// <summary>The Google-subject-like key of the test user. A real Google subject is numeric, so the prefix never collides.</summary>
    private const string SubjectPrefix = "e2e:";

    private const string EmailDomain = "e2e.invalid";

    public static bool IsValid(string? handle) =>
        handle is { Length: >= 1 and <= MaxLength } && handle.All(IsAllowedCharacter);

    public static string SubjectFor(string handle) => SubjectPrefix + handle;

    public static string EmailFor(string handle) => $"{handle}@{EmailDomain}";

    private static bool IsAllowedCharacter(char character) =>
        character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-';
}

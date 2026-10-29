using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SignalChat.Api.Chat.RateLimiting;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>The test sign-in is limited to 20 requests a minute everywhere, except the E2E runs, which need room for a full run.</summary>
public sealed class TestLoginLimitSettingsTests
{
    private const int DefaultTestLoginPerMinute = 20;
    private const int E2eTestLoginPerMinute = 200;
    private const string TestLoginKey = nameof(ChatRateLimitOptions.TestLoginPerMinute);

    [Fact]
    public void DefaultTestLoginLimit_StaysAtTwentyPerMinute()
    {
        Assert.Equal(DefaultTestLoginPerMinute, new ChatRateLimitOptions().TestLoginPerMinute);
    }

    [Fact]
    public void SharedSettingsFiles_DoNotChangeTheTestLoginLimit()
    {
        using var factory = new SignalChatApiFactory();
        var configuration = BuildFromContentRoot(factory, "appsettings.json", "appsettings.Development.json");

        Assert.Null(configuration.GetSection(ChatRateLimitOptions.SectionName).GetValue<int?>(TestLoginKey));
    }

    [Fact]
    public void E2eSettingsFile_RaisesTheTestLoginLimitForE2eOnly()
    {
        using var factory = new E2eEnvironmentApiFactory();
        var configuration = BuildFromContentRoot(factory, "appsettings.E2E.json");

        Assert.Equal(E2eTestLoginPerMinute, configuration.GetSection(ChatRateLimitOptions.SectionName).GetValue<int>(TestLoginKey));
    }

    /// <summary>Reads the named settings files from the API project, the content root the host itself uses.</summary>
    private static IConfiguration BuildFromContentRoot(SignalChatApiFactory factory, params string[] fileNames)
    {
        var contentRoot = factory.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        var builder = new ConfigurationBuilder().SetBasePath(contentRoot);
        foreach (var fileName in fileNames)
        {
            builder.AddJsonFile(fileName, optional: true);
        }
        return builder.Build();
    }
}

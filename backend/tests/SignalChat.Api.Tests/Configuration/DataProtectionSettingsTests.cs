using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Configuration;

namespace SignalChat.Api.Tests.Configuration;

public sealed class DataProtectionSettingsTests : IDisposable
{
    private readonly string _keysDirectory = Path.Combine(Path.GetTempPath(), "signalchat-dp-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_keysDirectory))
        {
            Directory.Delete(_keysDirectory, recursive: true);
        }
    }

    [Fact]
    public void Production_WithoutKeysPath_StopsTheHost()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => DataProtectionSettings.FromConfiguration(Empty(), isProduction: true));

        Assert.Contains(DataProtectionSettings.KeysPathKey, error.Message);
    }

    [Fact]
    public void Production_WhitespaceKeysPath_StopsTheHost()
    {
        var configuration = Configured(" ");

        Assert.Throws<InvalidOperationException>(() => DataProtectionSettings.FromConfiguration(configuration, isProduction: true));
    }

    [Fact]
    public void OutsideProduction_WithoutKeysPath_KeepsKeysInMemory()
    {
        var settings = DataProtectionSettings.FromConfiguration(Empty(), isProduction: false);

        Assert.Null(settings.KeysPath);
    }

    [Fact]
    public void KeysPath_IsTakenFromConfiguration()
    {
        var settings = DataProtectionSettings.FromConfiguration(Configured(_keysDirectory), isProduction: true);

        Assert.Equal(_keysDirectory, settings.KeysPath);
    }

    [Fact]
    public void KeysPath_PersistsTheKeyRingToTheDirectory()
    {
        var services = new ServiceCollection();
        var settings = DataProtectionSettings.FromConfiguration(Configured(_keysDirectory), isProduction: true);
        settings.ApplyTo(services.AddDataProtection());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test").Protect("payload");

        Assert.NotEmpty(Directory.GetFiles(_keysDirectory, "*.xml"));
    }

    private static IConfiguration Empty() => new ConfigurationBuilder().Build();

    private static IConfiguration Configured(string keysPath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DataProtectionSettings.KeysPathKey] = keysPath })
            .Build();
}

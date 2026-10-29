using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalChat.Api.Auth;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Chat;
using SignalChat.Api.Tests.Infrastructure;

namespace SignalChat.Api.Tests.Auth;

/// <summary>The test sign-in is limited per client address, and switching it on leaves a warning in the log.</summary>
public sealed class TestLoginRateLimitTests
{
    [Fact]
    public async Task TestLogin_IsLimitedPerClientAddress()
    {
        using var factory = new TightTestLoginApiFactory();
        using var client = factory.CreateApiClient();
        for (var index = 0; index < LowRateLimitApiFactory.TightRestPermitsPerMinute; index++)
        {
            using var allowed = await client.PostAsJsonAsync(TestLoginEndpoints.Route, new TestLoginRequest($"rl-login-{index}"));
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync(TestLoginEndpoints.Route, new TestLoginRequest("rl-login-over"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HubErrorCodes.RateLimited, (await ChatHttp.ReadJsonAsync(limited)).GetProperty("code").GetString());
    }

    [Fact]
    public void EnabledTestAuth_WritesAWarningAtStartup()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = new E2eTestAuthApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(logs))));
        using var client = factory.CreateClient();

        Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains(E2eTestAuthSettings.FlagKey));
    }

    /// <summary>Keeps the messages the host logs, so a test can look for one.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        public sealed record LogEntry(LogLevel Level, string Message);

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}

using System.Globalization;
using System.Security.Claims;
using SignalChat.Api.Auth;
using SignalChat.Api.Hubs;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Users;
using SignalChat.Infrastructure.Users;

namespace SignalChat.Api.Tests.Hubs;

/// <summary>The session re-check done when a hub connection opens.</summary>
public sealed class HubSessionGuardTests
{
    private const int IssuedVersion = 1;

    [Fact]
    public async Task CurrentVersion_IsValid()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var guard = new HubSessionGuard(new FakeAccounts(storedVersion: IssuedVersion), clock);

        var valid = await guard.IsStillValidAsync(Principal(IssuedVersion, clock.GetUtcNow()), CancellationToken.None);

        Assert.True(valid);
    }

    [Fact]
    public async Task RevokedVersion_IsRefused()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var guard = new HubSessionGuard(new FakeAccounts(storedVersion: IssuedVersion + 1), clock);

        var valid = await guard.IsStillValidAsync(Principal(IssuedVersion, clock.GetUtcNow()), CancellationToken.None);

        Assert.False(valid);
    }

    [Fact]
    public async Task MissingUser_IsRefused()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var guard = new HubSessionGuard(new FakeAccounts(storedVersion: null), clock);

        var valid = await guard.IsStillValidAsync(Principal(IssuedVersion, clock.GetUtcNow()), CancellationToken.None);

        Assert.False(valid);
    }

    [Fact]
    public async Task SessionOlderThanTheAbsoluteLifetime_IsRefused()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var startedAt = clock.GetUtcNow();
        var guard = new HubSessionGuard(new FakeAccounts(storedVersion: IssuedVersion), clock);
        clock.Advance(SessionPolicy.AbsoluteSessionLifetime + TimeSpan.FromSeconds(1));

        var valid = await guard.IsStillValidAsync(Principal(IssuedVersion, startedAt), CancellationToken.None);

        Assert.False(valid);
    }

    [Fact]
    public async Task MalformedClaims_AreRefused()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var guard = new HubSessionGuard(new FakeAccounts(storedVersion: IssuedVersion), clock);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], AuthSchemes.Session));

        var valid = await guard.IsStillValidAsync(principal, CancellationToken.None);

        Assert.False(valid);
    }

    private static ClaimsPrincipal Principal(int version, DateTimeOffset startedAt) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(SessionPolicy.SessionVersionClaim, version.ToString(CultureInfo.InvariantCulture)),
                new Claim(SessionPolicy.SessionStartedClaim, startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ],
            AuthSchemes.Session));

    /// <summary>Only the session version lookup is exercised; the other members are never reached here.</summary>
    private sealed class FakeAccounts(int? storedVersion) : IUserAccountService
    {
        public Task<int?> FindSessionVersionAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(storedVersion);

        public Task<User> SignInWithGoogleAsync(GoogleProfile profile, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

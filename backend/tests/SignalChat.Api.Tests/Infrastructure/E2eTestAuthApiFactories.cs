using SignalChat.Api.Auth;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>The API with <c>E2E_TEST_AUTH=true</c> in the Testing environment: <c>POST /auth/test-login</c> is mapped.</summary>
public sealed class E2eTestAuthApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;
}

/// <summary>The test sign-in with the limit lowered to two requests a minute, to reach the limit in three calls.</summary>
public sealed class TightTestLoginApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;

    protected override int RestPermitsPerMinute => LowRateLimitApiFactory.TightRestPermitsPerMinute;
}

/// <summary>The former local-run environment is no longer allowed for the test sign-in: the host must refuse to start.</summary>
public sealed class E2eDevelopmentApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;

    protected override string EnvironmentName => "Development";
}

/// <summary>ASP.NET Core Development without the test sign-in: how the stage environment runs (Swagger on).</summary>
public sealed class DevelopmentApiFactory : SignalChatApiFactory
{
    protected override string EnvironmentName => "Development";
}

/// <summary>The E2E environment with its own settings file, but without the flag: reads <c>appsettings.E2E.json</c> from the API project.</summary>
public sealed class E2eEnvironmentApiFactory : SignalChatApiFactory
{
    protected override string EnvironmentName => "E2E";
}

/// <summary>The forbidden combination: <c>E2E_TEST_AUTH=true</c> in Production. The host must refuse to start.</summary>
public sealed class E2eProductionApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;

    protected override string EnvironmentName => "Production";
}

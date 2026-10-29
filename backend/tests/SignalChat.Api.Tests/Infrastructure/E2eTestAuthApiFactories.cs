using SignalChat.Api.Auth;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>The API with <c>E2E_TEST_AUTH=true</c> in the Testing environment: <c>POST /auth/test-login</c> is mapped.</summary>
public sealed class E2eTestAuthApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;
}

/// <summary>The forbidden combination: <c>E2E_TEST_AUTH=true</c> in Production. The host must refuse to start.</summary>
public sealed class E2eProductionApiFactory : SignalChatApiFactory
{
    protected override bool E2eTestAuthEnabled => true;

    protected override string EnvironmentName => "Production";
}

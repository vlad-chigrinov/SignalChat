namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>Same API with every REST limit at <see cref="TightRestPermitsPerMinute"/>, so a test reaches the limit in three calls.</summary>
public sealed class LowRateLimitApiFactory : SignalChatApiFactory
{
    public const int TightRestPermitsPerMinute = 2;

    protected override int RestPermitsPerMinute => TightRestPermitsPerMinute;
}

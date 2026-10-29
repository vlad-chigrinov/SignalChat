using SignalChat.Api.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Tests.Chat;

public sealed class MessageCursorTests
{
    [Fact]
    public void Cursor_RoundTrips()
    {
        var original = new MessageCursor(new DateTimeOffset(2026, 10, 29, 12, 30, 15, 123, TimeSpan.Zero), Guid.NewGuid());

        var parsed = MessageCursor.TryParse(original.Encode(), out var cursor);

        Assert.True(parsed);
        Assert.Equal(original, cursor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%%%")]
    [InlineData("bm90IGEgY3Vyc29y")]
    [InlineData("MTIzOm5vdC1hLWd1aWQ")]
    public void Cursor_RejectsMalformedValues(string value)
    {
        Assert.False(MessageCursor.TryParse(value, out _));
    }

    [Fact]
    public void Cursor_RejectsTicksOutsideTheDateRange()
    {
        var plain = $"{long.MaxValue}:{Guid.NewGuid():D}";
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain)).TrimEnd('=');

        Assert.False(MessageCursor.TryParse(encoded, out _));
    }

    [Theory]
    [InlineData(null, 30, 100, 30)]
    [InlineData(5, 30, 100, 5)]
    [InlineData(500, 30, 100, 100)]
    public void Pagination_UsesDefaultAndCapsAtTheMaximum(int? requested, int defaultLimit, int maxLimit, int expected)
    {
        Assert.Equal(expected, Pagination.Clamp(requested, defaultLimit, maxLimit));
    }

    [Fact]
    public void Pagination_BelowOne_IsAnError()
    {
        var error = Assert.Throws<SignalChat.Domain.Chat.ChatRuleException>(() => Pagination.Clamp(0, 30, 100));

        Assert.Equal("invalid_limit", error.Code);
    }
}

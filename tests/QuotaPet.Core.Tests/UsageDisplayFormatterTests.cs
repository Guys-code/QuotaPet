using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class UsageDisplayFormatterTests
{
    [Fact]
    public void FormatsPercentAndTokenCounts()
    {
        Assert.Equal("79%", UsageDisplayFormatter.Percent(79.4));
        Assert.Equal("4,787,846", UsageDisplayFormatter.Tokens(4_787_846));
    }

    [Fact]
    public void Countdown_FormatsFutureAndExpiredReset()
    {
        var now = new DateTimeOffset(2026, 9, 18, 1, 0, 0, TimeSpan.Zero);

        Assert.Equal("1小时 2分钟", UsageDisplayFormatter.Countdown(now.AddHours(1).AddMinutes(2), now));
        Assert.Equal("已重置", UsageDisplayFormatter.Countdown(now.AddSeconds(-1), now));
        Assert.Equal("未知", UsageDisplayFormatter.Countdown(null, now));
    }
}

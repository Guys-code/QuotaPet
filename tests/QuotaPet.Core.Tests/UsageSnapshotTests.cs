using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class UsageSnapshotTests
{
    [Theory]
    [InlineData(21, 79)]
    [InlineData(-5, 100)]
    [InlineData(140, 0)]
    public void RemainingPercent_IsClamped(double used, double expected)
    {
        var window = new RateLimitWindow(used, 300, null);

        Assert.Equal(expected, window.RemainingPercent);
    }

    [Fact]
    public void Empty_HasNoUsageData()
    {
        Assert.False(UsageSnapshot.Empty.HasData);
        Assert.Null(UsageSnapshot.Empty.Primary);
        Assert.Equal(0, UsageSnapshot.Empty.SessionTokens.TotalTokens);
    }
}

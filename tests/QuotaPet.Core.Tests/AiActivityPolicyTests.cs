using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class AiActivityPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void RecentUsage_IsActive() =>
        Assert.True(AiActivityPolicy.IsActive(Now.AddSeconds(-44), Now));

    [Fact]
    public void UsageAtTimeoutBoundary_IsResting() =>
        Assert.False(AiActivityPolicy.IsActive(Now.AddSeconds(-45), Now));

    [Fact]
    public void MissingUsage_IsResting() =>
        Assert.False(AiActivityPolicy.IsActive(null, Now));

    [Fact]
    public void FutureClockSkew_DoesNotKeepAnimationActive() =>
        Assert.False(AiActivityPolicy.IsActive(Now.AddMinutes(1), Now));
}

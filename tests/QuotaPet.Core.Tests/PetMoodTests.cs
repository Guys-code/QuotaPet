using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class PetMoodTests
{
    [Fact]
    public void EmptySnapshot_IsWaiting() =>
        Assert.Equal(PetMood.Waiting, PetMoodPolicy.GetMood(UsageSnapshot.Empty));

    [Theory]
    [InlineData(79, PetMood.Normal)]
    [InlineData(21, PetMood.Normal)]
    [InlineData(20, PetMood.Tired)]
    [InlineData(5, PetMood.Tired)]
    [InlineData(4.9, PetMood.Sleeping)]
    public void RemainingQuota_SelectsMood(double remaining, PetMood expected)
    {
        var snapshot = Snapshot(100 - remaining);
        Assert.Equal(expected, PetMoodPolicy.GetMood(snapshot));
    }

    [Fact]
    public void ReachedLimit_IsSleeping()
    {
        var snapshot = Snapshot(10) with { RateLimitReachedType = "primary" };
        Assert.Equal(PetMood.Sleeping, PetMoodPolicy.GetMood(snapshot));
    }

    [Theory]
    [InlineData(10.01, PetMood.Normal)]
    [InlineData(10, PetMood.Tired)]
    [InlineData(1, PetMood.Tired)]
    [InlineData(0.99, PetMood.Sleeping)]
    public void DeepSeekCnyBalance_SelectsMatchingMood(double balance, PetMood expected)
    {
        var snapshot = DeepSeekSnapshot((decimal)balance);
        Assert.Equal(expected, PetMoodPolicy.GetDeepSeekMood(snapshot));
    }

    [Fact]
    public void DeepSeekWithoutBalance_IsWaiting() =>
        Assert.Equal(PetMood.Waiting, PetMoodPolicy.GetDeepSeekMood(null));

    [Theory]
    [InlineData(UsageDisplayMode.GptPlusOnly, PetMood.Sleeping)]
    [InlineData(UsageDisplayMode.DeepSeekOnly, PetMood.Normal)]
    [InlineData(UsageDisplayMode.All, PetMood.Normal)]
    public void DisplayMode_SelectsRequestedOrHealthierMood(UsageDisplayMode mode, PetMood expected)
    {
        var gpt = Snapshot(96);
        var deepSeek = DeepSeekSnapshot(20);

        Assert.Equal(expected, PetMoodPolicy.GetMood(mode, gpt, deepSeek));
    }

    [Fact]
    public void AllMode_PrefersKnownMoodWhenOtherServiceIsWaiting()
    {
        Assert.Equal(
            PetMood.Sleeping,
            PetMoodPolicy.GetMood(UsageDisplayMode.All, Snapshot(96), null));
    }

    private static UsageSnapshot Snapshot(double used) => new(
        DateTimeOffset.UtcNow, new(), new(), null,
        new RateLimitWindow(used, 300, null), null, new(), "plus", null);

    private static DeepSeekBalanceSnapshot DeepSeekSnapshot(decimal balance) => new(
        DateTimeOffset.UtcNow,
        true,
        [new DeepSeekBalance("CNY", balance, 0, balance)]);
}

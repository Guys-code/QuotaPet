using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class DeepSeekSpendTrackerTests
{
    [Fact]
    public void Update_FirstObservationStartsAtZeroSpend()
    {
        var state = DeepSeekSpendTracker.Update(null, "key-a", [Balance("CNY", 20m)]);

        Assert.Equal(20m, state.Currencies["CNY"].LastBalance);
        Assert.Equal(0m, state.Currencies["CNY"].Spent);
    }

    [Fact]
    public void Update_BalanceDropAddsToSpend()
    {
        var prior = new DeepSeekLedgerState("key-a", new Dictionary<string, DeepSeekCurrencyLedger>
        {
            ["CNY"] = new(20m, 1.5m)
        });

        var state = DeepSeekSpendTracker.Update(prior, "key-a", [Balance("CNY", 17.25m)]);

        Assert.Equal(17.25m, state.Currencies["CNY"].LastBalance);
        Assert.Equal(4.25m, state.Currencies["CNY"].Spent);
    }

    [Fact]
    public void Update_TopUpDoesNotReduceAccumulatedSpend()
    {
        var prior = new DeepSeekLedgerState("key-a", new Dictionary<string, DeepSeekCurrencyLedger>
        {
            ["CNY"] = new(5m, 3m)
        });

        var state = DeepSeekSpendTracker.Update(prior, "key-a", [Balance("CNY", 55m)]);

        Assert.Equal(55m, state.Currencies["CNY"].LastBalance);
        Assert.Equal(3m, state.Currencies["CNY"].Spent);
    }

    [Fact]
    public void Update_NewKeyResetsLedger()
    {
        var prior = new DeepSeekLedgerState("key-a", new Dictionary<string, DeepSeekCurrencyLedger>
        {
            ["CNY"] = new(20m, 8m)
        });

        var state = DeepSeekSpendTracker.Update(prior, "key-b", [Balance("CNY", 12m)]);

        Assert.Equal(0m, state.Currencies["CNY"].Spent);
    }

    [Fact]
    public void Update_BalanceDropCreatesTimestampedSpendEvent()
    {
        var prior = new DeepSeekLedgerState("key-a", new Dictionary<string, DeepSeekCurrencyLedger>
        {
            ["CNY"] = new(20m, 0m)
        });
        var observedAt = new DateTimeOffset(2026, 9, 18, 10, 30, 0, TimeSpan.FromHours(8));

        var state = DeepSeekSpendTracker.Update(prior, "key-a", [Balance("CNY", 18.75m)], observedAt);

        var spend = Assert.Single(state.Events!);
        Assert.Equal(1.25m, spend.Amount);
        Assert.Equal("CNY", spend.Currency);
        Assert.Equal(observedAt, spend.ObservedAt);
    }

    [Fact]
    public void Summarize_SeparatesRecentTodayWeekAndMonth()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.FromHours(8));
        var state = new DeepSeekLedgerState("key-a", new(),
        [
            new(now.AddHours(-2), "CNY", 1m),
            new(now.AddHours(-5), "CNY", 2m),
            new(now.AddDays(-2), "CNY", 4m),
            new(now.AddDays(-10), "CNY", 8m),
            new(now.AddMonths(-1), "CNY", 16m)
        ]);

        var summary = DeepSeekSpendTracker.Summarize(state, "CNY", now);

        Assert.Equal(1m, summary.LastThreeHours);
        Assert.Equal(3m, summary.Today);
        Assert.Equal(7m, summary.ThisWeek);
        Assert.Equal(15m, summary.ThisMonth);
    }

    private static DeepSeekBalance Balance(string currency, decimal total) => new(currency, total, 0m, total);
}

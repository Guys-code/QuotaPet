using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class DeepSeekOfficialCostImporterTests
{
    [Fact]
    public void Parse_ReadsCurrentOfficialCostCsvAndMakesChargesPositive()
    {
        const string csv = """
            start_time_iso,end_time_iso,model,wallet_type,cost,currency
            2026-09-17T00:00:00+08:00,2026-09-18T00:00:00+08:00,deepseek-flash,Paid,-1.25,CNY
            """;

        var result = DeepSeekOfficialCostImporter.Parse(csv);

        var entry = Assert.Single(result);
        Assert.Equal(1.25m, entry.Amount);
        Assert.Equal("CNY", entry.Currency);
        Assert.Equal(DateTimeOffset.Parse("2026-09-17T00:00:00+08:00"), entry.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-09-18T00:00:00+08:00"), entry.End);
        Assert.False(string.IsNullOrWhiteSpace(entry.Id));
    }

    [Fact]
    public void Merge_ReimportDoesNotDuplicateAndOfficialRangeReplacesLiveSpend()
    {
        var start = DateTimeOffset.Parse("2026-09-17T00:00:00+08:00");
        var official = new DeepSeekOfficialCostEntry("row-1", start, start.AddDays(1), "CNY", 3m);
        var ledger = new DeepSeekLedgerState(
            "key",
            new Dictionary<string, DeepSeekCurrencyLedger> { ["CNY"] = new(10m, 1m) },
            [new DeepSeekSpendEvent(start.AddHours(18), "CNY", 1m)]);

        var once = DeepSeekOfficialCostImporter.Merge(ledger, [official]);
        var twice = DeepSeekOfficialCostImporter.Merge(once, [official]);
        var summary = DeepSeekSpendTracker.Summarize(
            twice,
            "CNY",
            DateTimeOffset.Parse("2026-09-17T20:00:00+08:00"),
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));

        Assert.Single(twice.OfficialCosts!);
        Assert.Equal(3m, summary.Today);
        Assert.Equal(1m, summary.LastThreeHours);
    }

    [Fact]
    public void Parse_SupportsQuotedFieldsAndLegacyUtcDate()
    {
        const string csv = """
            utc_date,model,wallet_type,cost,currency
            2026-09-16,"deepseek,flash",Paid,-0.50,CNY
            """;

        var entry = Assert.Single(DeepSeekOfficialCostImporter.Parse(csv));

        Assert.Equal(0.50m, entry.Amount);
        Assert.Equal(TimeSpan.FromDays(1), entry.End - entry.Start);
    }
}

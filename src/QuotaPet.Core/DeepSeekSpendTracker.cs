using System.Security.Cryptography;
using System.Text;

namespace QuotaPet.Core;

public sealed record DeepSeekCurrencyLedger(decimal LastBalance, decimal Spent);

public sealed record DeepSeekSpendEvent(DateTimeOffset ObservedAt, string Currency, decimal Amount);

public sealed record DeepSeekOfficialCostEntry(
    string Id,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Currency,
    decimal Amount);

public sealed record DeepSeekSpendSummary(
    decimal LastThreeHours,
    decimal Today,
    decimal ThisWeek,
    decimal ThisMonth);

public sealed record DeepSeekLedgerState(
    string KeyFingerprint,
    Dictionary<string, DeepSeekCurrencyLedger> Currencies,
    List<DeepSeekSpendEvent>? Events = null,
    List<DeepSeekOfficialCostEntry>? OfficialCosts = null);

public static class DeepSeekSpendTracker
{
    public static DeepSeekLedgerState Update(
        DeepSeekLedgerState? previous,
        string keyFingerprint,
        IReadOnlyList<DeepSeekBalance> balances,
        DateTimeOffset? observedAt = null)
    {
        var sameKey = previous?.KeyFingerprint == keyFingerprint;
        var currencies = new Dictionary<string, DeepSeekCurrencyLedger>(StringComparer.OrdinalIgnoreCase);
        var events = sameKey ? previous!.Events?.ToList() ?? [] : [];
        var officialCosts = previous?.OfficialCosts?.ToList() ?? [];
        var timestamp = observedAt ?? DateTimeOffset.Now;

        foreach (var balance in balances)
        {
            var spent = 0m;
            if (sameKey && previous!.Currencies.TryGetValue(balance.Currency, out var prior))
            {
                var deduction = prior.LastBalance - balance.TotalBalance;
                spent = prior.Spent + Math.Max(0m, deduction);
                if (deduction > 0m) events.Add(new DeepSeekSpendEvent(timestamp, balance.Currency, deduction));
            }
            currencies[balance.Currency] = new DeepSeekCurrencyLedger(balance.TotalBalance, spent);
        }

        return new DeepSeekLedgerState(keyFingerprint, currencies, events, officialCosts);
    }

    public static DeepSeekSpendSummary Summarize(
        DeepSeekLedgerState? ledger,
        string currency,
        DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null)
    {
        var current = now ?? DateTimeOffset.Now;
        var zone = timeZone ?? TimeZoneInfo.Local;
        var localNow = TimeZoneInfo.ConvertTime(current, zone);
        var today = AtStartOfDay(localNow.Date, zone);
        var daysFromMonday = ((int)localNow.DayOfWeek + 6) % 7;
        var weekStart = AtStartOfDay(localNow.Date.AddDays(-daysFromMonday), zone);
        var monthStart = AtStartOfDay(new DateTime(localNow.Year, localNow.Month, 1), zone);
        var official = (ledger?.OfficialCosts ?? [])
            .Where(x => x.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var live = (ledger?.Events ?? [])
            .Where(x => x.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        decimal LiveTotal(DateTimeOffset start, DateTimeOffset end, bool excludeOfficial)
        {
            return live.Where(x => x.ObservedAt >= start && x.ObservedAt <= end)
                .Where(x => !excludeOfficial || !official.Any(o => x.ObservedAt >= o.Start && x.ObservedAt < o.End))
                .Sum(x => x.Amount);
        }

        decimal CombinedTotal(DateTimeOffset start) =>
            LiveTotal(start, current, true) + official
                .Where(x => x.Start >= start && x.Start <= current)
                .Sum(x => x.Amount);

        return new DeepSeekSpendSummary(
            LiveTotal(current.AddHours(-3), current, false),
            CombinedTotal(today),
            CombinedTotal(weekStart),
            CombinedTotal(monthStart));
    }

    private static DateTimeOffset AtStartOfDay(DateTime date, TimeZoneInfo zone) =>
        new(date, zone.GetUtcOffset(date));

    public static string Fingerprint(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }
}

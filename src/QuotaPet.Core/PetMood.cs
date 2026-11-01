namespace QuotaPet.Core;

public enum PetMood
{
    Waiting,
    Normal,
    Tired,
    Sleeping
}

public static class PetMoodPolicy
{
    public static PetMood GetMood(UsageSnapshot snapshot)
    {
        if (!snapshot.HasData) return PetMood.Waiting;
        if (!string.IsNullOrWhiteSpace(snapshot.RateLimitReachedType)) return PetMood.Sleeping;

        var remaining = new[] { snapshot.Primary?.RemainingPercent, snapshot.Secondary?.RemainingPercent }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(100)
            .Min();

        if (remaining < 5) return PetMood.Sleeping;
        if (remaining <= 20) return PetMood.Tired;
        return PetMood.Normal;
    }

    public static PetMood GetDeepSeekMood(DeepSeekBalanceSnapshot? snapshot)
    {
        if (snapshot is null) return PetMood.Waiting;
        if (!snapshot.IsAvailable) return PetMood.Sleeping;

        var cnyBalances = snapshot.Balances
            .Where(balance => balance.Currency.Equals("CNY", StringComparison.OrdinalIgnoreCase) ||
                              balance.Currency.Equals("RMB", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (cnyBalances.Length == 0) return PetMood.Waiting;

        var total = cnyBalances.Sum(balance => balance.TotalBalance);
        if (total < 1m) return PetMood.Sleeping;
        if (total <= 10m) return PetMood.Tired;
        return PetMood.Normal;
    }

    public static PetMood GetMood(
        UsageDisplayMode displayMode,
        UsageSnapshot gptPlus,
        DeepSeekBalanceSnapshot? deepSeek)
    {
        var gptMood = GetMood(gptPlus);
        var deepSeekMood = GetDeepSeekMood(deepSeek);
        return displayMode switch
        {
            UsageDisplayMode.GptPlusOnly => gptMood,
            UsageDisplayMode.DeepSeekOnly => deepSeekMood,
            _ => HealthRank(gptMood) >= HealthRank(deepSeekMood) ? gptMood : deepSeekMood
        };
    }

    private static int HealthRank(PetMood mood) => mood switch
    {
        PetMood.Normal => 3,
        PetMood.Tired => 2,
        PetMood.Sleeping => 1,
        _ => 0
    };
}

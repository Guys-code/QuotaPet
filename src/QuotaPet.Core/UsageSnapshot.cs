namespace QuotaPet.Core;

public sealed record TokenBreakdown(
    long InputTokens = 0,
    long CachedInputTokens = 0,
    long CacheWriteInputTokens = 0,
    long OutputTokens = 0,
    long ReasoningOutputTokens = 0,
    long TotalTokens = 0);

public sealed record RateLimitWindow(
    double UsedPercent,
    int WindowMinutes,
    DateTimeOffset? ResetsAt)
{
    public double RemainingPercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

public sealed record CreditStatus(bool HasCredits = false, bool Unlimited = false, string? Balance = null);

public sealed record UsageSnapshot(
    DateTimeOffset ObservedAt,
    TokenBreakdown SessionTokens,
    TokenBreakdown LastTokens,
    long? ModelContextWindow,
    RateLimitWindow? Primary,
    RateLimitWindow? Secondary,
    CreditStatus Credits,
    string? PlanType,
    string? RateLimitReachedType)
{
    public static UsageSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue,
        new TokenBreakdown(),
        new TokenBreakdown(),
        null,
        null,
        null,
        new CreditStatus(),
        null,
        null);

    public bool HasData => ObservedAt != DateTimeOffset.MinValue;
}

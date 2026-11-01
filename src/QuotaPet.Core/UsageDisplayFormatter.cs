using System.Globalization;

namespace QuotaPet.Core;

public static class UsageDisplayFormatter
{
    public static string Percent(double value) => $"{Math.Clamp(value, 0, 100):0}%";

    public static string Tokens(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Countdown(DateTimeOffset? resetAt, DateTimeOffset? now = null)
    {
        if (resetAt is null) return "未知";
        var remaining = resetAt.Value - (now ?? DateTimeOffset.Now);
        if (remaining <= TimeSpan.Zero) return "已重置";
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}天 {remaining.Hours}小时";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}小时 {remaining.Minutes}分钟";
        return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}分钟";
    }

    public static string LocalResetTime(DateTimeOffset? resetAt) =>
        resetAt?.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "未知";
}

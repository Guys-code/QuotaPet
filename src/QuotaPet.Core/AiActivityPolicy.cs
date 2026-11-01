namespace QuotaPet.Core;

public static class AiActivityPolicy
{
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(45);

    public static bool IsActive(DateTimeOffset? lastActivity, DateTimeOffset now)
    {
        if (lastActivity is null) return false;
        var elapsed = now - lastActivity.Value;
        return elapsed >= TimeSpan.Zero && elapsed < ActiveWindow;
    }
}

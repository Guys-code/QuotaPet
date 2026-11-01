using System.Text.Json;

namespace QuotaPet.Core;

public enum UsageDisplayMode
{
    All,
    GptPlusOnly,
    DeepSeekOnly
}

public sealed record AppSettings(
    int X,
    int Y,
    bool AnimationEnabled,
    DeepSeekLedgerState? DeepSeekLedger = null,
    UsageDisplayMode DisplayMode = UsageDisplayMode.All)
{
    public static AppSettings Default { get; } = new(-1, -1, true);
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return AppSettings.Default;
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? AppSettings.Default;
        }
        catch (IOException) { return AppSettings.Default; }
        catch (UnauthorizedAccessException) { return AppSettings.Default; }
        catch (JsonException) { return AppSettings.Default; }
    }

    public static AppSettings LoadWithMigration(string path, string legacyPath)
    {
        if (File.Exists(path)) return Load(path);
        if (!File.Exists(legacyPath)) return AppSettings.Default;

        var settings = Load(legacyPath);
        try
        {
            Save(path, settings);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return settings;
    }

    public static void Save(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }
}

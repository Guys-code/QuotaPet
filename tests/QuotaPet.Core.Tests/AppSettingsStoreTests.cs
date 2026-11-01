using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "QuotaPetTests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void SaveAndLoad_RoundTripsValues()
    {
        var expected = new AppSettings(123, 456, false);

        AppSettingsStore.Save(_path, expected);
        var actual = AppSettingsStore.Load(_path);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_ReturnsDefaultsForCorruptFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "not-json");

        Assert.Equal(AppSettings.Default, AppSettingsStore.Load(_path));
    }

    [Fact]
    public void LoadWithMigration_MovesLegacySettingsToNewPath()
    {
        var root = Path.GetDirectoryName(_path)!;
        var legacyPath = Path.Combine(root, "legacy", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        AppSettingsStore.Save(legacyPath, new AppSettings(31, 42, false, DisplayMode: UsageDisplayMode.DeepSeekOnly));

        var settings = AppSettingsStore.LoadWithMigration(_path, legacyPath);

        Assert.Equal(31, settings.X);
        Assert.Equal(42, settings.Y);
        Assert.False(settings.AnimationEnabled);
        Assert.Equal(UsageDisplayMode.DeepSeekOnly, settings.DisplayMode);
        Assert.True(File.Exists(_path));
        Assert.Equal(settings, AppSettingsStore.Load(_path));
    }

    [Fact]
    public void LoadWithMigration_PrefersExistingSettingsAtNewPath()
    {
        var root = Path.GetDirectoryName(_path)!;
        var legacyPath = Path.Combine(root, "legacy", "settings.json");
        AppSettingsStore.Save(legacyPath, new AppSettings(10, 20, false));
        AppSettingsStore.Save(_path, new AppSettings(30, 40, true));

        var settings = AppSettingsStore.LoadWithMigration(_path, legacyPath);

        Assert.Equal(30, settings.X);
        Assert.Equal(40, settings.Y);
        Assert.True(settings.AnimationEnabled);
    }

    [Fact]
    public void Load_OldSettingsWithoutDeepSeekLedger_RemainsCompatible()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """{"X":10,"Y":20,"AnimationEnabled":true}""");

        var settings = AppSettingsStore.Load(_path);

        Assert.Equal(10, settings.X);
        Assert.Null(settings.DeepSeekLedger);
        Assert.Equal(UsageDisplayMode.All, settings.DisplayMode);
    }

    [Fact]
    public void SaveAndLoad_PreservesDisplayMode()
    {
        var expected = new AppSettings(123, 456, true, DisplayMode: UsageDisplayMode.DeepSeekOnly);

        AppSettingsStore.Save(_path, expected);
        var actual = AppSettingsStore.Load(_path);

        Assert.Equal(UsageDisplayMode.DeepSeekOnly, actual.DisplayMode);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

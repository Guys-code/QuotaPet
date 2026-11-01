using QuotaPet.Core;
using System.Text.Json;

namespace QuotaPet.Core.Tests;

public sealed class CodexUsageReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuotaPetTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadLatest_ReturnsNewestValidTokenEventAcrossNestedFiles()
    {
        var older = Path.Combine(_root, "2026", "09", "17", "older.jsonl");
        var newer = Path.Combine(_root, "2026", "09", "18", "newer.jsonl");
        WriteLines(older, Event(10, "2026-09-17T01:00:00Z"));
        WriteLines(newer, Event(20, "2026-09-18T01:00:00Z"), "{unfinished");

        var snapshot = CodexUsageReader.ReadLatest(_root);

        Assert.NotNull(snapshot);
        Assert.Equal(20, snapshot.SessionTokens.TotalTokens);
    }

    [Fact]
    public void ReadLatest_ReturnsNullForMissingDirectory()
    {
        Assert.Null(CodexUsageReader.ReadLatest(_root));
    }

    [Fact]
    public async Task Monitor_EmitsSnapshotWhenValidLineIsAppended()
    {
        var path = Path.Combine(_root, "2026", "09", "18", "active.jsonl");
        WriteLines(path, Event(10, "2026-09-18T01:00:00Z"));
        using var monitor = new CodexSessionMonitor(_root, TimeSpan.FromMilliseconds(40));
        var completion = new TaskCompletionSource<UsageSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.UsageUpdated += (_, snapshot) =>
        {
            if (snapshot.SessionTokens.TotalTokens == 99) completion.TrySetResult(snapshot);
        };
        monitor.Start();

        await File.AppendAllTextAsync(path, Event(99, "2026-09-18T01:01:00Z") + Environment.NewLine);
        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(99, result.SessionTokens.TotalTokens);
    }

    [Fact]
    public async Task Monitor_RecoversWhenFileIsTruncatedAndRewritten()
    {
        var path = Path.Combine(_root, "active.jsonl");
        WriteLines(path, Event(5000, "2026-09-18T01:00:00Z"));
        using var monitor = new CodexSessionMonitor(_root, TimeSpan.FromMilliseconds(40));
        var completion = new TaskCompletionSource<UsageSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.UsageUpdated += (_, snapshot) =>
        {
            if (snapshot.SessionTokens.TotalTokens == 7) completion.TrySetResult(snapshot);
        };
        monitor.Start();

        await File.WriteAllTextAsync(path, Event(7, "2026-09-18T01:02:00Z") + Environment.NewLine);
        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(7, result.SessionTokens.TotalTokens);
    }

    private static string Event(long total, string timestamp) => JsonSerializer.Serialize(new
    {
        timestamp,
        type = "event_msg",
        payload = new
        {
            type = "token_count",
            info = new { total_token_usage = new { total_tokens = total } }
        }
    });

    private static void WriteLines(string path, params string[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

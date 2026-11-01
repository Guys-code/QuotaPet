using System.Text.Json;
using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class ClaudeDeepSeekUsageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuotaPetTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Parser_ReadsDeepSeekClaudeUsage()
    {
        var line = Event("msg-1", "deepseek-v4-flash", "2026-09-18T01:00:00Z", 10, 3, 20, 40);

        var parsed = ClaudeDeepSeekEventParser.TryParse(line, out var usage);

        Assert.True(parsed);
        Assert.Equal(10, usage.Tokens.InputTokens);
        Assert.Equal(3, usage.Tokens.OutputTokens);
        Assert.Equal(20, usage.Tokens.CacheCreationInputTokens);
        Assert.Equal(40, usage.Tokens.CacheReadInputTokens);
        Assert.Equal(73, usage.Tokens.TotalTokens);
    }

    [Fact]
    public void Parser_IgnoresNonDeepSeekModels()
    {
        var line = Event("msg-1", "mimo-v2.5", "2026-09-18T01:00:00Z", 10, 3, 0, 0);

        Assert.False(ClaudeDeepSeekEventParser.TryParse(line, out _));
    }

    [Fact]
    public void Reader_DeduplicatesRepeatedMessageIdsAndChoosesLargestUsage()
    {
        var path = Path.Combine(_root, "project", "session.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path,
        [
            Event("msg-1", "deepseek-v4-flash", "2026-09-18T01:00:00Z", 10, 2, 0, 5),
            Event("msg-1", "deepseek-v4-flash", "2026-09-18T01:00:01Z", 12, 4, 0, 5),
            Event("msg-2", "deepseek-flash", "2026-09-18T02:00:00Z", 20, 8, 0, 0)
        ]);

        var snapshot = ClaudeDeepSeekUsageReader.Read(_root, new DateOnly(2026, 9, 18));

        Assert.Equal(2, snapshot.RequestCount);
        Assert.Equal(32, snapshot.Total.InputTokens);
        Assert.Equal(12, snapshot.Total.OutputTokens);
        Assert.Equal(5, snapshot.Total.CacheReadInputTokens);
        Assert.Equal(49, snapshot.Total.TotalTokens);
        Assert.Equal(28, snapshot.LastRequest.TotalTokens);
    }

    private static string Event(string id, string model, string timestamp, long input, long output, long cacheCreation, long cacheRead) =>
        JsonSerializer.Serialize(new
        {
            timestamp,
            type = "assistant",
            message = new
            {
                id,
                model,
                usage = new
                {
                    input_tokens = input,
                    output_tokens = output,
                    cache_creation_input_tokens = cacheCreation,
                    cache_read_input_tokens = cacheRead
                }
            }
        });

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

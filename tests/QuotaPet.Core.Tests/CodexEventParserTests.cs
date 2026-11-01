using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class CodexEventParserTests
{
    private const string CompleteEvent = """
        {"timestamp":"2026-09-18T01:23:45Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":1000,"cached_input_tokens":800,"cache_write_input_tokens":2,"output_tokens":200,"reasoning_output_tokens":50,"total_tokens":1200},"last_token_usage":{"input_tokens":100,"cached_input_tokens":80,"cache_write_input_tokens":1,"output_tokens":20,"reasoning_output_tokens":5,"total_tokens":120},"model_context_window":258400},"rate_limits":{"primary":{"used_percent":21.0,"window_minutes":300,"resets_at":1789682407},"secondary":{"used_percent":7.0,"window_minutes":10080,"resets_at":1789806078},"credits":{"has_credits":false,"unlimited":false,"balance":"0"},"plan_type":"plus","rate_limit_reached_type":null}}}
        """;

    [Fact]
    public void TryParseTokenCount_MapsCompleteEvent()
    {
        var parsed = CodexEventParser.TryParseTokenCount(CompleteEvent, out var snapshot);

        Assert.True(parsed);
        Assert.Equal(1000, snapshot.SessionTokens.InputTokens);
        Assert.Equal(800, snapshot.SessionTokens.CachedInputTokens);
        Assert.Equal(50, snapshot.SessionTokens.ReasoningOutputTokens);
        Assert.Equal(120, snapshot.LastTokens.TotalTokens);
        Assert.Equal(258400, snapshot.ModelContextWindow);
        Assert.Equal(21, snapshot.Primary!.UsedPercent);
        Assert.Equal(300, snapshot.Primary.WindowMinutes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1789682407), snapshot.Primary.ResetsAt);
        Assert.Equal(7, snapshot.Secondary!.UsedPercent);
        Assert.Equal("plus", snapshot.PlanType);
        Assert.False(snapshot.Credits.HasCredits);
        Assert.Equal("0", snapshot.Credits.Balance);
        Assert.Equal(DateTimeOffset.Parse("2026-09-18T01:23:45Z"), snapshot.ObservedAt);
    }

    [Theory]
    [InlineData("{\"type\":\"event_msg\",\"payload\":{\"type\":\"item_completed\"}}")]
    [InlineData("{truncated")]
    [InlineData("")]
    public void TryParseTokenCount_RejectsUnrelatedOrInvalidLines(string line)
    {
        Assert.False(CodexEventParser.TryParseTokenCount(line, out _));
    }

    [Fact]
    public void TryParseTokenCount_AllowsMissingOptionalFields()
    {
        const string line = """
            {"type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":42}}}}
            """;

        Assert.True(CodexEventParser.TryParseTokenCount(line, out var snapshot));
        Assert.Equal(42, snapshot.SessionTokens.TotalTokens);
        Assert.Null(snapshot.Primary);
        Assert.Null(snapshot.Secondary);
    }

    // Codex writes `model_context_window` (and `rate_limits`) as an explicit JSON null rather
    // than omitting them. Absent and null are different ValueKinds, and JsonElement.TryGetInt64
    // throws InvalidOperationException on a non-Number element instead of returning false.
    private const string NullOptionalFieldsEvent = """
        {"timestamp":"2026-10-13T18:26:22Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":0,"cached_input_tokens":0,"cache_write_input_tokens":0,"output_tokens":0,"reasoning_output_tokens":0,"total_tokens":199},"last_token_usage":{"input_tokens":0,"cached_input_tokens":0,"cache_write_input_tokens":0,"output_tokens":0,"reasoning_output_tokens":0,"total_tokens":199},"model_context_window":null},"rate_limits":null}}
        """;

    [Fact]
    public void TryParseTokenCount_AcceptsNullOptionalFields()
    {
        Assert.True(CodexEventParser.TryParseTokenCount(NullOptionalFieldsEvent, out var snapshot));
        Assert.Null(snapshot.ModelContextWindow);
        Assert.Equal(199, snapshot.SessionTokens.TotalTokens);
        Assert.Equal(199, snapshot.LastTokens.TotalTokens);
        Assert.Null(snapshot.Primary);
        Assert.Null(snapshot.Secondary);
    }

    // Same defect class as above, one field deeper: a rate-limit window that is present but
    // carries a null number. Exercises GetDouble, GetInt32 and GetInt64 respectively.
    [Theory]
    [InlineData("{\"used_percent\":null,\"window_minutes\":300}")]
    [InlineData("{\"used_percent\":21.0,\"window_minutes\":null}")]
    [InlineData("{\"used_percent\":21.0,\"window_minutes\":300,\"resets_at\":null}")]
    public void TryParseTokenCount_AcceptsNullNumericFieldsInsidePresentWindow(string primary)
    {
        var line = "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":7}},\"rate_limits\":{\"primary\":" + primary + "}}}";

        Assert.True(CodexEventParser.TryParseTokenCount(line, out var snapshot));
        Assert.Equal(7, snapshot.SessionTokens.TotalTokens);
    }
}

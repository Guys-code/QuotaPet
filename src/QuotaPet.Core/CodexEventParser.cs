using System.Globalization;
using System.Text.Json;

namespace QuotaPet.Core;

public static class CodexEventParser
{
    public static bool TryParseTokenCount(string line, out UsageSnapshot snapshot)
    {
        snapshot = UsageSnapshot.Empty;
        if (string.IsNullOrWhiteSpace(line)) return false;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (GetString(root, "type") != "event_msg" ||
                !root.TryGetProperty("payload", out var payload) ||
                GetString(payload, "type") != "token_count")
            {
                return false;
            }

            var observedAt = DateTimeOffset.UtcNow;
            var timestamp = GetString(root, "timestamp");
            if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTime))
            {
                observedAt = parsedTime;
            }

            payload.TryGetProperty("info", out var info);
            payload.TryGetProperty("rate_limits", out var limits);
            var session = ParseTokens(GetObject(info, "total_token_usage"));
            var last = ParseTokens(GetObject(info, "last_token_usage"));
            var contextWindow = GetInt64(info, "model_context_window");
            var primary = ParseWindow(GetObject(limits, "primary"));
            var secondary = ParseWindow(GetObject(limits, "secondary"));
            var creditsElement = GetObject(limits, "credits");
            var credits = new CreditStatus(
                GetBoolean(creditsElement, "has_credits") ?? false,
                GetBoolean(creditsElement, "unlimited") ?? false,
                GetString(creditsElement, "balance"));

            snapshot = new UsageSnapshot(
                observedAt,
                session,
                last,
                contextWindow,
                primary,
                secondary,
                credits,
                GetString(limits, "plan_type"),
                GetString(limits, "rate_limit_reached_type"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TokenBreakdown ParseTokens(JsonElement element) => new(
        GetInt64(element, "input_tokens") ?? 0,
        GetInt64(element, "cached_input_tokens") ?? 0,
        GetInt64(element, "cache_write_input_tokens") ?? 0,
        GetInt64(element, "output_tokens") ?? 0,
        GetInt64(element, "reasoning_output_tokens") ?? 0,
        GetInt64(element, "total_tokens") ?? 0);

    private static RateLimitWindow? ParseWindow(JsonElement element)
    {
        var used = GetDouble(element, "used_percent");
        var minutes = GetInt32(element, "window_minutes");
        if (used is null || minutes is null) return null;
        var resetSeconds = GetInt64(element, "resets_at");
        return new RateLimitWindow(used.Value, minutes.Value,
            resetSeconds is null ? null : DateTimeOffset.FromUnixTimeSeconds(resetSeconds.Value));
    }

    private static JsonElement GetObject(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // JsonElement.TryGetInt64/TryGetInt32/TryGetDouble throw InvalidOperationException when the
    // element is not a JSON number -- including an explicit null, which Codex writes for
    // model_context_window and rate_limits. Check the number kind first so these stay total.
    private static long? GetInt64(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var result)
            ? result
            : null;

    private static int? GetInt32(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static double? GetDouble(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var result)
            ? result
            : null;

    private static bool? GetBoolean(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}

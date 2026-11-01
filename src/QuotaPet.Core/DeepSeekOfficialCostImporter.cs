using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QuotaPet.Core;

public static class DeepSeekOfficialCostImporter
{
    public static IReadOnlyList<DeepSeekOfficialCostEntry> Parse(string csv)
    {
        using var reader = new StringReader(csv.TrimStart('\uFEFF'));
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)) return [];
        var headers = ParseLine(headerLine);
        var indexes = headers.Select((name, index) => (name, index))
            .ToDictionary(x => x.name.Trim(), x => x.index, StringComparer.OrdinalIgnoreCase);
        if (!indexes.ContainsKey("cost"))
            throw new FormatException("所选文件不是 DeepSeek 官方费用 CSV（缺少 cost 列）。");
        if (!indexes.ContainsKey("start_time_iso") && !indexes.ContainsKey("utc_date"))
            throw new FormatException("DeepSeek 费用 CSV 缺少时间列。");

        var result = new List<DeepSeekOfficialCostEntry>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseLine(line);
            string Field(string name) => indexes.TryGetValue(name, out var i) && i < fields.Count
                ? fields[i].Trim()
                : string.Empty;
            if (!decimal.TryParse(Field("cost"), NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var rawCost) || rawCost == 0m) continue;
            var startText = Field("start_time_iso");
            if (string.IsNullOrEmpty(startText)) startText = Field("utc_date");
            if (!DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var start)) continue;
            var endText = Field("end_time_iso");
            var end = DateTimeOffset.TryParse(endText, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsedEnd) ? parsedEnd : start.AddDays(1);
            if (end <= start) end = start.AddDays(1);
            var currency = Field("currency");
            if (string.IsNullOrWhiteSpace(currency)) currency = "CNY";
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line.Trim())));
            result.Add(new DeepSeekOfficialCostEntry(id, start, end, currency, Math.Abs(rawCost)));
        }
        return result;
    }

    public static DeepSeekLedgerState Merge(
        DeepSeekLedgerState ledger,
        IEnumerable<DeepSeekOfficialCostEntry> imported)
    {
        var entries = new Dictionary<string, DeepSeekOfficialCostEntry>(StringComparer.Ordinal);
        foreach (var item in ledger.OfficialCosts ?? []) entries[item.Id] = item;
        foreach (var item in imported) entries[item.Id] = item;
        return ledger with { OfficialCosts = entries.Values.OrderBy(x => x.Start).ToList() };
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else value.Append(c);
        }
        fields.Add(value.ToString());
        return fields;
    }
}

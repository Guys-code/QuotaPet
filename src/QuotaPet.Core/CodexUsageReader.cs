namespace QuotaPet.Core;

public static class CodexUsageReader
{
    public static UsageSnapshot? ReadLatest(string sessionsRoot)
    {
        if (!Directory.Exists(sessionsRoot)) return null;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(12)
                .ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        UsageSnapshot? latest = null;
        foreach (var file in files)
        {
            var snapshot = ReadLatestSnapshot(file);
            if (snapshot is not null && (latest is null || snapshot.ObservedAt >= latest.ObservedAt)) latest = snapshot;
        }

        return latest;
    }

    internal static UsageSnapshot? ReadLatestSnapshot(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            const int tailBytes = 256 * 1024;
            var start = Math.Max(0, stream.Length - tailBytes);
            stream.Seek(start, SeekOrigin.Begin);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var text = System.Text.Encoding.UTF8.GetString(memory.ToArray());
            if (start > 0)
            {
                var firstNewLine = text.IndexOf('\n');
                if (firstNewLine < 0) return null;
                text = text[(firstNewLine + 1)..];
            }

            var lines = text.Split('\n');
            for (var index = lines.Length - 1; index >= 0; index--)
                if (CodexEventParser.TryParseTokenCount(lines[index].TrimEnd('\r'), out var snapshot))
                    return snapshot;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }
}

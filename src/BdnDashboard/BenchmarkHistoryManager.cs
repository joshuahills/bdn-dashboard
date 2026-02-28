using System.Globalization;
using System.Text.Json;

namespace BdnDashboard;

/// <summary>
/// Reads BenchmarkDotNet JSON reports and maintains a history file for trend tracking.
/// </summary>
public static class BenchmarkHistoryManager
{
    public record BenchmarkDataPoint(
        string RunId,
        DateTime Timestamp,
        string BenchmarkName,
        string Category,
        string Parameters,
        double MeanNs,
        double MedianNs,
        double StdDevNs,
        long BytesAllocated,
        int Gen0,
        int Gen1,
        int Gen2);

    public record BenchmarkHistory(List<BenchmarkDataPoint> DataPoints);

    public static BenchmarkHistory LoadHistory(string historyPath)
    {
        if (!File.Exists(historyPath))
            return new BenchmarkHistory([]);

        var json = File.ReadAllText(historyPath);
        var points = JsonSerializer.Deserialize<List<BenchmarkDataPoint>>(json, JsonOptions) ?? [];
        return new BenchmarkHistory(points);
    }

    public static void SaveHistory(BenchmarkHistory history, string historyPath)
    {
        var json = JsonSerializer.Serialize(history.DataPoints, JsonOptions);
        var dir = Path.GetDirectoryName(historyPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(historyPath, json);
    }

    public static List<BenchmarkDataPoint> ParseBdnResults(string resultsDir, string runId, DateTime timestamp)
    {
        var points = new List<BenchmarkDataPoint>();
        var jsonFiles = Directory.GetFiles(resultsDir, "*-report-full.json");

        foreach (var file in jsonFiles)
        {
            var json = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.GetProperty("Title").GetString() ?? "";
            var category = ExtractCategory(title);

            var benchmarks = root.GetProperty("Benchmarks");
            foreach (var benchmark in benchmarks.EnumerateArray())
            {
                var method = benchmark.GetProperty("Method").GetString() ?? "";
                var methodTitle = benchmark.GetProperty("MethodTitle").GetString() ?? method;
                var parameters = benchmark.GetProperty("Parameters").GetString() ?? "";
                var displayName = string.IsNullOrEmpty(parameters)
                    ? methodTitle
                    : $"{methodTitle} [{parameters}]";

                var stats = benchmark.GetProperty("Statistics");
                var meanNs = stats.GetProperty("Mean").GetDouble();
                var medianNs = stats.GetProperty("Median").GetDouble();
                var stdDevNs = stats.GetProperty("StandardDeviation").GetDouble();

                var memory = benchmark.GetProperty("Memory");
                var bytesAllocated = memory.GetProperty("BytesAllocatedPerOperation").GetInt64();
                var gen0 = memory.GetProperty("Gen0Collections").GetInt32();
                var gen1 = memory.GetProperty("Gen1Collections").GetInt32();
                var gen2 = memory.GetProperty("Gen2Collections").GetInt32();

                points.Add(new BenchmarkDataPoint(
                    runId, timestamp, displayName, category, parameters,
                    meanNs, medianNs, stdDevNs, bytesAllocated, gen0, gen1, gen2));
            }
        }

        return points;
    }

    private static string ExtractCategory(string title)
    {
        // Title format: "Namespace.ClassName-timestamp"
        // Try to extract the class name and strip "Benchmarks" suffix
        var lastDot = title.LastIndexOf('.');
        if (lastDot >= 0)
        {
            var remainder = title[(lastDot + 1)..];
            var dashIdx = remainder.IndexOf('-');
            var className = dashIdx >= 0 ? remainder[..dashIdx] : remainder;
            return className.Replace("Benchmarks", "");
        }
        return "Unknown";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

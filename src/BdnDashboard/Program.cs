using System.Globalization;

namespace BdnDashboard;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        var resultsDir = GetArg(args, "--results") ?? "BenchmarkDotNet.Artifacts/results";
        var historyPath = GetArg(args, "--history") ?? "BenchmarkDotNet.Artifacts/benchmark-history.json";
        var outputPath = GetArg(args, "--output") ?? "BenchmarkDotNet.Artifacts/benchmark-report.html";
        var runId = GetArg(args, "--run-id") ?? DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var title = GetArg(args, "--title") ?? "Performance Trends";

        if (!Directory.Exists(resultsDir))
        {
            Console.Error.WriteLine($"Error: Results directory not found: {resultsDir}");
            Console.Error.WriteLine("Run benchmarks first to generate BenchmarkDotNet JSON output.");
            return 1;
        }

        var jsonFiles = Directory.GetFiles(resultsDir, "*-report-full.json");
        if (jsonFiles.Length == 0)
        {
            Console.Error.WriteLine($"Error: No *-report-full.json files found in: {resultsDir}");
            Console.Error.WriteLine("Ensure BenchmarkDotNet is configured with JsonExporter.Full.");
            return 1;
        }

        Console.WriteLine($"Loading history from: {historyPath}");
        var history = BenchmarkHistoryManager.LoadHistory(historyPath);
        Console.WriteLine($"  Existing data points: {history.DataPoints.Count}");

        Console.WriteLine($"Parsing {jsonFiles.Length} benchmark result file(s)...");
        var newPoints = BenchmarkHistoryManager.ParseBdnResults(resultsDir, runId, DateTime.UtcNow);
        Console.WriteLine($"  New data points: {newPoints.Count}");

        history.DataPoints.AddRange(newPoints);

        Console.WriteLine($"Saving history to: {historyPath}");
        BenchmarkHistoryManager.SaveHistory(history, historyPath);

        Console.WriteLine($"Generating report: {outputPath}");
        HtmlReportGenerator.Generate(history, outputPath, title);

        var runCount = history.DataPoints.Select(d => d.RunId).Distinct().Count();
        Console.WriteLine($"Done. {runCount} run(s) in history, {history.DataPoints.Count} total data points.");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            BdnDashboard - BenchmarkDotNet trend dashboard generator

            Usage: bdn-dashboard [options]

            Options:
              --results <dir>    BenchmarkDotNet results directory
                                 (default: BenchmarkDotNet.Artifacts/results)
              --history <file>   History JSON file path
                                 (default: BenchmarkDotNet.Artifacts/benchmark-history.json)
              --output <file>    Output HTML report path
                                 (default: BenchmarkDotNet.Artifacts/benchmark-report.html)
              --run-id <id>      Run identifier, e.g. git SHA or build number
                                 (default: timestamp)
              --title <text>     Dashboard title
                                 (default: "Performance Trends")
              -h, --help         Show this help

            Examples:
              bdn-dashboard
              bdn-dashboard --run-id "v1.2.0" --title "MyLib Performance"
              bdn-dashboard --results ./artifacts/results --output ./report.html
            """);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}

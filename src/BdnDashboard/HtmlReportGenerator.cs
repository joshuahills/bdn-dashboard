using System.Globalization;
using System.Text;
using System.Text.Json;
using static BdnDashboard.BenchmarkHistoryManager;

namespace BdnDashboard;

/// <summary>
/// Generates a self-contained HTML report with Chart.js trend charts from benchmark history.
/// </summary>
public static class HtmlReportGenerator
{
    public static void Generate(BenchmarkHistory history, string outputPath, string title = "Performance Trends")
    {
        var grouped = history.DataPoints
            .GroupBy(dp => dp.Category)
            .OrderBy(g => g.Key)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(HtmlHead(title));

        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");
        sb.AppendLine($"<h1>{Escape(title)}</h1>");
        sb.AppendLine($"<p class=\"subtitle\">Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC &mdash; {history.DataPoints.Select(d => d.RunId).Distinct().Count()} run(s) tracked</p>");

        // Summary table of latest run
        var latestRun = history.DataPoints
            .OrderByDescending(d => d.Timestamp)
            .GroupBy(d => d.RunId)
            .FirstOrDefault();

        if (latestRun != null)
        {
            sb.AppendLine("<h2>Latest Run Summary</h2>");
            sb.AppendLine("<table><thead><tr><th>Category</th><th>Benchmark</th><th>Mean</th><th>Median</th><th>StdDev</th><th>Allocated</th></tr></thead><tbody>");
            foreach (var dp in latestRun.OrderBy(d => d.Category).ThenBy(d => d.BenchmarkName))
            {
                sb.AppendLine($"<tr><td>{Escape(dp.Category)}</td><td>{Escape(dp.BenchmarkName)}</td><td>{FormatTime(dp.MeanNs)}</td><td>{FormatTime(dp.MedianNs)}</td><td>{FormatTime(dp.StdDevNs)}</td><td>{FormatBytes(dp.BytesAllocated)}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Trend charts per category
        if (history.DataPoints.Select(d => d.RunId).Distinct().Count() > 1)
        {
            sb.AppendLine("<h2>Trends Over Time</h2>");
        }
        else
        {
            sb.AppendLine("<h2>Baseline Snapshot</h2>");
            sb.AppendLine("<p class=\"hint\">Run benchmarks again with a different <code>--run-id</code> to see trends.</p>");
        }

        int chartIdx = 0;
        foreach (var categoryGroup in grouped)
        {
            sb.AppendLine($"<h3>{Escape(categoryGroup.Key)}</h3>");
            sb.AppendLine("<div class=\"chart-row\">");

            var timeChartId = $"timeChart{chartIdx}";
            sb.AppendLine($"<div class=\"chart-container\"><canvas id=\"{timeChartId}\"></canvas></div>");

            string? memChartId = null;
            if (categoryGroup.Any(dp => dp.BytesAllocated > 0))
            {
                memChartId = $"memChart{chartIdx}";
                sb.AppendLine($"<div class=\"chart-container\"><canvas id=\"{memChartId}\"></canvas></div>");
            }

            sb.AppendLine("</div>");

            var benchmarkNames = categoryGroup
                .Select(dp => dp.BenchmarkName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var runIds = categoryGroup
                .OrderBy(dp => dp.Timestamp)
                .Select(dp => dp.RunId)
                .Distinct()
                .ToList();

            var labels = JsonSerializer.Serialize(runIds);

            // Time datasets
            sb.AppendLine("<script>");
            sb.AppendLine($"createTimeChart('{timeChartId}', {labels}, [");
            int colorIdx = 0;
            foreach (var name in benchmarkNames)
            {
                var values = new List<string>();
                foreach (var runId in runIds)
                {
                    var dp = categoryGroup.FirstOrDefault(d => d.BenchmarkName == name && d.RunId == runId);
                    values.Add(dp != null ? dp.MeanNs.ToString("F2", CultureInfo.InvariantCulture) : "null");
                }
                var color = ChartColors[colorIdx % ChartColors.Length];
                sb.AppendLine($"  {{ label: {JsonSerializer.Serialize(name)}, data: [{string.Join(",", values)}], borderColor: '{color}', backgroundColor: '{color}20', tension: 0.3 }},");
                colorIdx++;
            }
            sb.AppendLine("]);");

            // Memory datasets
            if (memChartId != null)
            {
                sb.AppendLine($"createMemoryChart('{memChartId}', {labels}, [");
                colorIdx = 0;
                foreach (var name in benchmarkNames)
                {
                    var values = new List<string>();
                    foreach (var runId in runIds)
                    {
                        var dp = categoryGroup.FirstOrDefault(d => d.BenchmarkName == name && d.RunId == runId);
                        values.Add(dp != null ? dp.BytesAllocated.ToString(CultureInfo.InvariantCulture) : "null");
                    }
                    var color = ChartColors[colorIdx % ChartColors.Length];
                    sb.AppendLine($"  {{ label: {JsonSerializer.Serialize(name)}, data: [{string.Join(",", values)}], borderColor: '{color}', backgroundColor: '{color}40', tension: 0.3 }},");
                    colorIdx++;
                }
                sb.AppendLine("]);");
            }
            sb.AppendLine("</script>");

            chartIdx++;
        }

        sb.AppendLine("</div></body></html>");

        var dir = Path.GetDirectoryName(outputPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, sb.ToString());
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string FormatTime(double ns) => ns switch
    {
        < 1_000 => $"{ns:F2} ns",
        < 1_000_000 => $"{ns / 1_000:F2} &micro;s",
        < 1_000_000_000 => $"{ns / 1_000_000:F2} ms",
        _ => $"{ns / 1_000_000_000:F2} s"
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        0 => "-",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };

    private static readonly string[] ChartColors =
    [
        "#2563eb", "#dc2626", "#16a34a", "#ca8a04", "#9333ea",
        "#0891b2", "#e11d48", "#65a30d", "#c2410c", "#7c3aed"
    ];

    private static string HtmlHead(string title) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>{{title}}</title>
        <script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif; background: #f8fafc; color: #1e293b; padding: 2rem; }
          .container { max-width: 1400px; margin: 0 auto; }
          h1 { font-size: 1.75rem; margin-bottom: 0.25rem; }
          .subtitle { color: #64748b; margin-bottom: 2rem; font-size: 0.9rem; }
          .hint { color: #94a3b8; font-size: 0.85rem; margin-bottom: 1rem; }
          .hint code { background: #f1f5f9; padding: 0.1em 0.4em; border-radius: 3px; font-size: 0.85em; }
          h2 { font-size: 1.3rem; margin: 2rem 0 1rem; border-bottom: 2px solid #e2e8f0; padding-bottom: 0.5rem; }
          h3 { font-size: 1.1rem; margin: 1.5rem 0 0.75rem; color: #475569; }
          table { width: 100%; border-collapse: collapse; margin-bottom: 1.5rem; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
          th { background: #1e293b; color: white; padding: 0.75rem 1rem; text-align: left; font-weight: 500; font-size: 0.85rem; }
          td { padding: 0.6rem 1rem; border-bottom: 1px solid #f1f5f9; font-size: 0.85rem; font-variant-numeric: tabular-nums; }
          tr:hover td { background: #f8fafc; }
          .chart-row { display: flex; gap: 1.5rem; flex-wrap: wrap; }
          .chart-container { flex: 1; min-width: 500px; background: white; border-radius: 8px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
          canvas { width: 100% !important; }
        </style>
        <script>
        function createTimeChart(canvasId, labels, datasets) {
          new Chart(document.getElementById(canvasId), {
            type: 'line',
            data: { labels, datasets },
            options: {
              responsive: true,
              plugins: {
                title: { display: true, text: 'Execution Time (ns)', font: { size: 14 } },
                legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } },
                tooltip: {
                  callbacks: {
                    label: ctx => {
                      let v = ctx.parsed.y;
                      if (v < 1000) return ctx.dataset.label + ': ' + v.toFixed(2) + ' ns';
                      if (v < 1e6) return ctx.dataset.label + ': ' + (v/1e3).toFixed(2) + ' \u03BCs';
                      return ctx.dataset.label + ': ' + (v/1e6).toFixed(2) + ' ms';
                    }
                  }
                }
              },
              scales: {
                x: { title: { display: true, text: 'Run' } },
                y: { title: { display: true, text: 'Time (ns)' }, beginAtZero: false }
              }
            }
          });
        }
        function createMemoryChart(canvasId, labels, datasets) {
          new Chart(document.getElementById(canvasId), {
            type: 'bar',
            data: { labels, datasets },
            options: {
              responsive: true,
              plugins: {
                title: { display: true, text: 'Memory Allocated (bytes/op)', font: { size: 14 } },
                legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } },
                tooltip: {
                  callbacks: {
                    label: ctx => {
                      let v = ctx.parsed.y;
                      if (v < 1024) return ctx.dataset.label + ': ' + v + ' B';
                      if (v < 1048576) return ctx.dataset.label + ': ' + (v/1024).toFixed(1) + ' KB';
                      return ctx.dataset.label + ': ' + (v/1048576).toFixed(1) + ' MB';
                    }
                  }
                }
              },
              scales: {
                x: { title: { display: true, text: 'Run' } },
                y: { title: { display: true, text: 'Bytes' }, beginAtZero: true }
              }
            }
          });
        }
        </script>
        </head>
        """;
}

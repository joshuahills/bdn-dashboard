# BdnDashboard

A dotnet tool that generates interactive HTML trend dashboards from [BenchmarkDotNet](https://benchmarkdotnet.org/) JSON results.

Track CPU time and memory allocation trends across benchmark runs with zero configuration.

## Install

```bash
# From NuGet (once published)
dotnet tool install -g BdnDashboard

# From a local build
dotnet pack src/BdnDashboard/BdnDashboard.csproj -c Release
dotnet tool install -g BdnDashboard --add-source src/BdnDashboard/bin/Release
```

## Usage

Run your BenchmarkDotNet benchmarks with the `JsonExporter.Full` exporter, then generate the dashboard:

```bash
# Run benchmarks (ensure JsonExporter.Full is configured)
dotnet run -c Release --project MyBenchmarks

# Generate dashboard
bdn-dashboard --run-id "baseline"

# Make changes, re-run benchmarks, then:
bdn-dashboard --run-id "after-optimisation"

# Open the report
start BenchmarkDotNet.Artifacts/benchmark-report.html
```

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `--results <dir>` | `BenchmarkDotNet.Artifacts/results` | Directory containing `*-report-full.json` files |
| `--history <file>` | `BenchmarkDotNet.Artifacts/benchmark-history.json` | History file for accumulating results across runs |
| `--output <file>` | `BenchmarkDotNet.Artifacts/benchmark-report.html` | Output HTML report path |
| `--run-id <id>` | Timestamp | Label for this run (e.g. git SHA, version, build number) |
| `--title <text>` | `Performance Trends` | Dashboard title |

## How It Works

1. **Parses** BenchmarkDotNet `*-report-full.json` files from the results directory
2. **Appends** the parsed data points to a `benchmark-history.json` file
3. **Generates** a self-contained HTML report with:
   - Summary table of the latest run (mean, median, stddev, allocations)
   - Line charts showing execution time trends per benchmark category
   - Bar charts showing memory allocation trends (where applicable)

The history file accumulates results across runs, so each invocation adds a new data point to the trend charts.

## CI Integration

### Azure DevOps

```yaml
- script: dotnet tool install -g BdnDashboard
  displayName: Install BdnDashboard

- script: bdn-dashboard --run-id "$(Build.BuildNumber)" --title "MyProject Performance"
  displayName: Generate Dashboard

- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: BenchmarkDotNet.Artifacts
    ArtifactName: BenchmarkResults
```

### GitHub Actions

```yaml
- run: dotnet tool install -g BdnDashboard
- run: bdn-dashboard --run-id "${{ github.sha }}" --title "MyProject Performance"
- uses: actions/upload-artifact@v4
  with:
    name: benchmark-results
    path: BenchmarkDotNet.Artifacts/
```

## Requirements

- .NET 8.0 or later
- BenchmarkDotNet configured with `JsonExporter.Full` (and optionally `[MemoryDiagnoser]`)

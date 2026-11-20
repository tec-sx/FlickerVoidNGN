// Writes Mermaid charts of a BenchmarkDotNet run for the GitHub run summary: a bar chart per benchmark with this run's
// mean time for each parameter set, and a line chart per parameter set with main's history up to this run.
// Usage: dotnet run BenchmarkCharts.cs -- <joined -report-full-compressed.json> [<history .json>] [<commit>]
using System.Globalization;
using System.Text;
using System.Text.Json;

const int HistoryRuns = 30;

string reportPath = args[0];
string? historyPath = null;
if (args.Length > 1)
{
    historyPath = args[1];
}

string commit = "";
if (args.Length > 2)
{
    commit = args[2];
}

string reportText = File.ReadAllText(reportPath);
using JsonDocument report = JsonDocument.Parse(reportText);
List<Result> results = [];
foreach (JsonElement benchmark in report.RootElement.GetProperty("Benchmarks").EnumerateArray())
{
    string type = benchmark.GetProperty("Type").GetString()!;
    string method = benchmark.GetProperty("Method").GetString()!;
    string parameters = benchmark.GetProperty("Parameters").GetString() ?? "";
    string fullName = benchmark.GetProperty("FullName").GetString()!;
    double mean = benchmark.GetProperty("Statistics").GetProperty("Mean").GetDouble();
    string group = $"{type.Replace("Benchmarks", "", StringComparison.Ordinal)}.{method}";
    results.Add(new Result(group, Label(parameters), fullName, mean));
}

Dictionary<string, List<(string Commit, double Nanoseconds)>> history = ReadHistory(historyPath, commit);

StringBuilder summary = new();
summary.AppendLine("## Benchmark charts");
summary.AppendLine();
foreach (IGrouping<string, Result> group in results.GroupBy(result => result.Group))
{
    List<Result> cases = [.. group];
    double largest = cases.Max(result => result.Nanoseconds);
    (string unit, double scale) = PickUnit(largest);
    IEnumerable<string> labels = cases.Select(result => Quote(result.Label));
    IEnumerable<string> values = cases.Select(result => Format(result.Nanoseconds / scale));
    summary.AppendLine("```mermaid");
    summary.AppendLine("xychart-beta");
    summary.AppendLine($"    title \"{group.Key}, this run\"");
    summary.AppendLine($"    x-axis [{string.Join(", ", labels)}]");
    summary.AppendLine($"    y-axis \"Mean ({unit})\" 0 --> {Format(largest / scale * 1.1)}");
    summary.AppendLine($"    bar [{string.Join(", ", values)}]");
    summary.AppendLine("```");
    summary.AppendLine();
}

if (history.Count > 0)
{
    summary.AppendLine("<details><summary>History on main, up to this run</summary>");
    summary.AppendLine();
    foreach (Result result in results)
    {
        List<(string Commit, double Nanoseconds)> runs = history.GetValueOrDefault(result.FullName) ?? [];
        List<(string Commit, double Nanoseconds)> points = [.. runs.TakeLast(HistoryRuns - 1)];
        string current = "this run";
        if (commit.Length >= 7)
        {
            current = commit[..7];
        }

        points.Add((current, result.Nanoseconds));
        double largest = points.Max(point => point.Nanoseconds);
        (string unit, double scale) = PickUnit(largest);
        IEnumerable<string> labels = points.Select(point => Quote(point.Commit));
        IEnumerable<string> values = points.Select(point => Format(point.Nanoseconds / scale));
        summary.AppendLine("```mermaid");
        summary.AppendLine("xychart-beta");
        summary.AppendLine($"    title \"{result.Group} ({result.Label})\"");
        summary.AppendLine($"    x-axis [{string.Join(", ", labels)}]");
        summary.AppendLine($"    y-axis \"Mean ({unit})\" 0 --> {Format(largest / scale * 1.1)}");
        summary.AppendLine($"    line [{string.Join(", ", values)}]");
        summary.AppendLine("```");
        summary.AppendLine();
    }

    summary.AppendLine("</details>");
}

Console.Write(summary.ToString());
return 0;

// The history the github-action-benchmark action keeps: each run's commit and its benchmarks' times in nanoseconds,
// leaving out this commit's own run, which the report adds.
static Dictionary<string, List<(string Commit, double Nanoseconds)>> ReadHistory(string? path, string commit)
{
    Dictionary<string, List<(string Commit, double Nanoseconds)>> history = [];
    if (path is null || !File.Exists(path))
    {
        return history;
    }

    string text = File.ReadAllText(path);
    using JsonDocument document = JsonDocument.Parse(text);
    foreach (JsonProperty suite in document.RootElement.GetProperty("entries").EnumerateObject())
    {
        foreach (JsonElement run in suite.Value.EnumerateArray())
        {
            string id = run.GetProperty("commit").GetProperty("id").GetString() ?? "";
            if (id == commit)
            {
                continue;
            }

            string label = id;
            if (id.Length >= 7)
            {
                label = id[..7];
            }

            foreach (JsonElement bench in run.GetProperty("benches").EnumerateArray())
            {
                string name = bench.GetProperty("name").GetString()!;
                double value = bench.GetProperty("value").GetDouble();
                if (!history.TryGetValue(name, out List<(string Commit, double Nanoseconds)>? runs))
                {
                    runs = [];
                    history.Add(name, runs);
                }

                runs.Add((label, value));
            }
        }
    }

    return history;
}

// "Lights=16&Projection=Orthographic" becomes "16 Orthographic".
static string Label(string parameters)
{
    if (parameters.Length == 0)
    {
        return "default";
    }

    IEnumerable<string> values = parameters.Split('&').Select(pair => pair[(pair.IndexOf('=') + 1)..]);
    return string.Join(" ", values);
}

static (string Unit, double Scale) PickUnit(double nanoseconds)
{
    if (nanoseconds >= 1_000_000)
    {
        return ("ms", 1_000_000);
    }

    if (nanoseconds >= 1_000)
    {
        return ("μs", 1_000);
    }

    return ("ns", 1);
}

static string Quote(string text) => $"\"{text.Replace("\"", "'", StringComparison.Ordinal)}\"";

static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

/// <summary>One benchmark case of the run: its chart, its label on the chart and its mean time.</summary>
internal sealed record Result(string Group, string Label, string FullName, double Nanoseconds);

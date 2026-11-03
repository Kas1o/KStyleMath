using System.Diagnostics;
using System.Text.Json;
using KStyleMath;

internal sealed class PerfCase
{
    public string Name;
    public string Tex;
    public bool Display;
}

internal static class Program
{
    private const int WarmupPasses = 40;
    private const int MeasurePasses = 250;

    private static int Main(string[] args)
    {
        string path = args.Length > 0 && !args[0].StartsWith("--")
            ? args[0]
            : FindCorpus();
        if (path == null)
        {
            Console.Error.WriteLine("perf.json not found");
            return 1;
        }

        var cases = Load(path);

        Console.WriteLine("KStyleMath performance benchmark");
        Console.WriteLine("================================");
        Console.WriteLine($"Runtime      : .NET {Environment.Version}");
        Console.WriteLine($"OS           : {Environment.OSVersion}");
        Console.WriteLine($"Cores        : {Environment.ProcessorCount}");
        Console.WriteLine($"Corpus       : {cases.Count} formulas ({Path.GetFileName(path)})");
        Console.WriteLine($"Passes       : {MeasurePasses} after {WarmupPasses} warmup passes");
        Console.WriteLine();

        // Cold start (includes static initialization, JSON data loading and JIT of the hot path).
        GC.Collect();
        var cold = Stopwatch.StartNew();
        Katex.RenderToString(cases[0].Tex, SettingsFor(cases[0], "htmlAndMathml"));
        cold.Stop();
        Console.WriteLine($"Cold first render (incl. data init): {cold.Elapsed.TotalMilliseconds:0.00} ms");
        Console.WriteLine();

        if (args.Contains("--alloc"))
        {
            AllocationBreakdown(cases);
            return 0;
        }

        var rows = new List<Result>();
        rows.Add(Run(cases, "full", c => SettingsFor(c, "htmlAndMathml"), null));
        rows.Add(Run(cases, "html", c => SettingsFor(c, "html"), null));
        rows.Add(Run(cases, "mathml", c => SettingsFor(c, "mathml"), null));
        rows.Add(Run(cases, "display", c => SettingsFor(c, "htmlAndMathml"), true));
        rows.Add(Run(cases, "parse-only", c => SettingsFor(c, "htmlAndMathml"), null, parseOnly: true));

        Console.WriteLine();
        Console.WriteLine($"{"mode",-10} {"renders",10} {"total ms",10} {"ops/s",10} {"mean µs",9} {"median µs",10} {"p95 µs",9} {"p99 µs",9} {"bytes/op",10}");
        Console.WriteLine(new string('-', 103));
        foreach (var r in rows)
        {
            Console.WriteLine($"{r.Mode,-10} {r.Count,10:N0} {r.TotalMs,10:N1} {r.OpsPerSecond,10:N0} " +
                              $"{r.MeanUs,9:N2} {r.MedianUs,10:N2} {r.P95Us,9:N2} {r.P99Us,9:N2} {r.BytesPerOp,10:N0}");
        }

        return 0;
    }

    private static void AllocationBreakdown(List<PerfCase> cases)
    {
        Console.WriteLine("Allocation breakdown (build only; parse trees pre-computed)");
        Console.WriteLine($"{"stage",-14} {"allocs/op",12} {"bytes/op",12} {"µs/op",9}");
        Console.WriteLine(new string('-', 50));

        var parsed = cases.Select(c => (Case: c, Tree: ParseTree.Parse(c.Tex, SettingsFor(c, "htmlAndMathml")))).ToArray();
        Stage("parse", () =>
        {
            foreach (var c in cases) ParseTree.Parse(c.Tex, SettingsFor(c, "htmlAndMathml"));
            return 0;
        });
        Stage("html-dom", () =>
        {
            foreach (var (c, tree) in parsed) BuildTree.BuildHTMLTree(tree, SettingsFor(c, "html"));
            return 0;
        });
        Stage("html-markup", () =>
        {
            foreach (var (c, tree) in parsed) BuildTree.BuildHTMLTree(tree, SettingsFor(c, "html")).ToMarkup();
            return 0;
        });
        Stage("full-markup", () =>
        {
            foreach (var (c, tree) in parsed) BuildTree.Build(tree, c.Tex, SettingsFor(c, "htmlAndMathml")).ToMarkup();
            return 0;
        });
        Stage("mathml-markup", () =>
        {
            foreach (var (c, tree) in parsed) BuildTree.Build(tree, c.Tex, SettingsFor(c, "mathml")).ToMarkup();
            return 0;
        });

        void Stage(string name, Func<int> action)
        {
            for (int i = 0; i < 10; i++) action();
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 40; i++) action();
            sw.Stop();
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            long ops = cases.Count * 40L;
            Console.WriteLine($"{name,-14} {ops,12:N0} {(a1 - a0) / (double)ops,12:N0} {sw.Elapsed.TotalMilliseconds * 1000 / ops,9:N2}");
        }
    }
    private sealed class Result
    {
        public string Mode;
        public long Count;
        public double TotalMs;
        public double OpsPerSecond;
        public double MeanUs;
        public double MedianUs;
        public double P95Us;
        public double P99Us;
        public double BytesPerOp;
    }

    private static Result Run(List<PerfCase> cases, string mode, Func<PerfCase, Settings> settings,
        bool? displayOverride, bool parseOnly = false)
    {
        var prepared = cases.Select(c =>
        {
            var s = settings(c);
            if (displayOverride.HasValue) s.DisplayMode = displayOverride.Value;
            return (Case: c, Settings: s);
        }).ToArray();

        // Warmup.
        for (int pass = 0; pass < WarmupPasses; pass++)
            foreach (var (c, s) in prepared)
                Render(c, s, parseOnly);

        int total = cases.Count * MeasurePasses;
        var samples = new double[total];
        int idx = 0;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        var totalTimer = Stopwatch.StartNew();
        for (int pass = 0; pass < MeasurePasses; pass++)
        {
            foreach (var (c, s) in prepared)
            {
                long start = Stopwatch.GetTimestamp();
                Render(c, s, parseOnly);
                long end = Stopwatch.GetTimestamp();
                samples[idx++] = (end - start) * 1_000_000.0 / Stopwatch.Frequency;
            }
        }
        totalTimer.Stop();
        long allocEnd = GC.GetAllocatedBytesForCurrentThread();

        Array.Sort(samples);
        double totalUs = samples.Sum();
        return new Result
        {
            Mode = mode,
            Count = total,
            TotalMs = totalTimer.Elapsed.TotalMilliseconds,
            OpsPerSecond = total / totalTimer.Elapsed.TotalSeconds,
            MeanUs = totalUs / total,
            MedianUs = Percentile(samples, 0.50),
            P95Us = Percentile(samples, 0.95),
            P99Us = Percentile(samples, 0.99),
            BytesPerOp = (allocEnd - allocStart) / (double)total,
        };
    }

    private static void Render(PerfCase c, Settings settings, bool parseOnly)
    {
        if (parseOnly)
            Katex.GenerateParseTree(c.Tex, settings);
        else
            Katex.RenderToString(c.Tex, settings);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        int index = (int)Math.Ceiling(p * sorted.Length) - 1;
        if (index < 0) index = 0;
        if (index >= sorted.Length) index = sorted.Length - 1;
        return sorted[index];
    }

    private static Settings SettingsFor(PerfCase c, string output) => new()
    {
        DisplayMode = c.Display,
        Output = output,
        Strict = "ignore",
        ThrowOnError = false,
    };

    private static List<PerfCase> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<PerfCase>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add(new PerfCase
            {
                Name = item.TryGetProperty("name", out var n) ? n.GetString() : "",
                Tex = item.GetProperty("tex").GetString(),
                Display = item.TryGetProperty("display", out var d) && d.GetBoolean(),
            });
        }
        return list;
    }

    private static string FindCorpus()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var p = Path.Combine(dir, "corpus", "perf.json");
            if (File.Exists(p)) return p;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}



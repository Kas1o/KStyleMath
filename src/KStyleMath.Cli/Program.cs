using System.Text.Json;
using KStyleMath;

if (args.Length > 0 && args[0] == "--lex")
{
    var lx = new Lexer(args[1], new Settings());
    for (int i = 0; i < 20; i++)
    {
        var t = lx.Lex();
        Console.WriteLine($"[{t.Text}] len={t.Text.Length}");
        if (t.Text == "EOF") break;
    }
    return 0;
}

if (args.Length > 0 && args[0] == "--expr")
{
        var settings = new Settings { DisplayMode = args.Length > 1 && args[1] == "--display" };
    var expr = args[1] == "--display" ? args[2] : args[1];
    Console.WriteLine(Katex.RenderToString(expr, settings));
    return 0;
}

string corpusPath = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : FindCorpus();
if (corpusPath == null)
{
    Console.Error.WriteLine("Usage: KStyleMath.Cli [goldens.json] [--name NAME] [--verbose]");
    return 1;
}

string nameFilter = null;
bool verbose = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--name" && i + 1 < args.Length) nameFilter = args[++i];
    if (args[i] == "--verbose") verbose = true;
}

using var doc = JsonDocument.Parse(File.ReadAllText(corpusPath));
int pass = 0, fail = 0, errors = 0;
var failures = new List<(string Name, string Actual, string Expected, string Error)>();
foreach (var item in doc.RootElement.EnumerateArray())
{
    string name = item.GetProperty("name").GetString();
    if (nameFilter != null && name != nameFilter) continue;
    string tex = item.GetProperty("tex").GetString();
    bool display = item.TryGetProperty("display", out var displayJson) && displayJson.GetBoolean();
    string expected = item.GetProperty("html").GetString();
    string actual;
    try
    {
        var st = new Settings { DisplayMode = display, ThrowOnError = true, Strict = "ignore" };
        if (item.TryGetProperty("settings", out var settingsJson))
        {
            if (settingsJson.TryGetProperty("displayMode", out var dm)) st.DisplayMode = dm.GetBoolean();
            if (settingsJson.TryGetProperty("output", out var op)) st.Output = op.GetString();
            if (settingsJson.TryGetProperty("leqno", out var lq)) st.Leqno = lq.GetBoolean();
            if (settingsJson.TryGetProperty("fleqn", out var fq)) st.Fleqn = fq.GetBoolean();
            if (settingsJson.TryGetProperty("colorIsTextColor", out var ct)) st.ColorIsTextColor = ct.GetBoolean();
            if (settingsJson.TryGetProperty("minRuleThickness", out var mrt)) st.MinRuleThickness = mrt.GetDouble();
            if (settingsJson.TryGetProperty("maxSize", out var ms)) st.MaxSize = ms.GetDouble();
            if (settingsJson.TryGetProperty("strict", out var sc)) st.Strict = sc.ValueKind == JsonValueKind.String ? sc.GetString() : sc.GetBoolean();
            if (settingsJson.TryGetProperty("throwOnError", out var toe)) st.ThrowOnError = toe.GetBoolean();
            if (settingsJson.TryGetProperty("errorColor", out var ec)) st.ErrorColor = ec.GetString();
            if (settingsJson.TryGetProperty("maxExpand", out var me)) st.MaxExpand = me.GetDouble();
            if (settingsJson.TryGetProperty("macros", out var macrosJson) && macrosJson.ValueKind == JsonValueKind.Object)
                foreach (var mp in macrosJson.EnumerateObject()) st.Macros[mp.Name] = mp.Value.GetString();
            if (settingsJson.TryGetProperty("trust", out var tr)) st.Trust = tr.ValueKind == JsonValueKind.True;
        }
        actual = Katex.RenderToString(tex, st);
    }
    catch (Exception ex)
    {
        if (expected.StartsWith("THROW:") && "THROW:" + ex.Message == expected)
        {
            pass++;
            continue;
        }
        errors++;
        failures.Add((name, "", expected, ex.ToString()));
        if (failures.Count <= 12) Console.WriteLine($"[ERROR] {name}: {ex.GetType().Name}: {ex.Message}"); if (verbose) Console.WriteLine(ex.ToString());
        continue;
    }
    if (actual == expected)
    {
        pass++;
    }
    else
    {
        fail++;
        failures.Add((name, actual, expected, null));
        if (failures.Count <= 12)
        {
            int idx = FirstDiff(actual, expected);
            Console.WriteLine($"[FAIL] {name} (char {idx})");
            if (verbose)
            {
                Console.WriteLine("  actual:   " + Snippet(actual, idx));
                Console.WriteLine("  expected: " + Snippet(expected, idx));
            }
        }
    }
}

Console.WriteLine();
Console.WriteLine($"Pass {pass} / {pass + fail + errors}  (fail {fail}, errors {errors})");
Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(corpusPath))!, "diffs"));
foreach (var f in failures)
{
    var safe = string.Concat(f.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(corpusPath))!, "diffs", safe + ".txt"),
        $"TEX: {f.Name}\nERROR: {f.Error}\nACTUAL:\n{f.Actual}\n\nEXPECTED:\n{f.Expected}\n");
}
return fail + errors == 0 ? 0 : 1;

static string FindCorpus()
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 8 && dir != null; i++)
    {
        var p = Path.Combine(dir, "corpus", "goldens.json");
        if (File.Exists(p)) return p;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static int FirstDiff(string a, string b)
{
    int n = Math.Min(a.Length, b.Length);
    for (int i = 0; i < n; i++) if (a[i] != b[i]) return i;
    return n;
}

static string Snippet(string s, int idx)
{
    int start = Math.Max(0, idx - 60);
    int len = Math.Min(160, s.Length - start);
    return (start > 0 ? "…" : "") + s.Substring(start, len) + (start + len < s.Length ? "…" : "");
}











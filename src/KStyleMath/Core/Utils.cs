using System.Text.RegularExpressions;

namespace KStyleMath;

public enum StrictMode
{
    False,
    True,
    Ignore,
    Warn,
    Error,
}

public static class StrictHelper
{
    public static bool IsTrue(object strict) => strict switch
    {
        bool b => b,
        string s => s == "error" || s == "warn" ? s == "error" : false,
        StrictMode m => m is StrictMode.True or StrictMode.Error,
        _ => false,
    };

    public static void Handle(object strict, string errorCode, string errorMsg, object token, bool report)
    {
        HandleBool(strict, errorCode, errorMsg, token, report);
    }

    public static bool HandleBool(object strict, string errorCode, string errorMsg, object token, bool report)
    {
        object behavior = strict;
        if (strict is StrictFunction fn)
        {
            try { behavior = fn(errorCode, errorMsg, token); }
            catch { if (!report) behavior = StrictMode.Error; else throw; }
        }
        else if (strict is StrictMode m)
        {
            behavior = m;
        }

        bool isError = behavior is true or StrictMode.True or StrictMode.Error ||
                       (behavior is string es && es == "error");
        bool isIgnore = behavior is false or StrictMode.False or StrictMode.Ignore ||
                        (behavior is string ss && ss == "ignore");
        if (isError)
        {
            if (report)
                throw new ParseError("LaTeX-incompatible input and strict mode is set to 'error': " +
                                     $"{errorMsg} [{errorCode}]", token);
            return true;
        }
        if (isIgnore)
        {
            return false;
        }
        // "warn" (also the default)
        Console.Error.WriteLine("LaTeX-incompatible input and strict mode is set to 'warn': " +
                                $"{errorMsg} [{errorCode}]");
        return false;
    }
}

public delegate object StrictFunction(string errorCode, string errorMsg, object token);

public sealed class Measurement
{
    public double Number;
    public string Unit;
    public Measurement(double number, string unit)
    {
        Number = number;
        Unit = unit;
    }
}

public static class Units
{
    public static readonly Dictionary<string, double> PtPerUnit = new(StringComparer.Ordinal)
    {
        ["pt"] = 1,
        ["mm"] = 7227.0 / 2540,
        ["cm"] = 7227.0 / 254,
        ["in"] = 72.27,
        ["bp"] = 803.0 / 800,
        ["pc"] = 12,
        ["dd"] = 1238.0 / 1157,
        ["cc"] = 14856.0 / 1157,
        ["nd"] = 685.0 / 642,
        ["nc"] = 1370.0 / 107,
        ["sp"] = 1.0 / 65536,
        ["px"] = 803.0 / 800,
    };

    private static readonly HashSet<string> RelativeUnit = new(StringComparer.Ordinal) { "ex", "em", "mu" };

    public static bool ValidUnit(string unit) => PtPerUnit.ContainsKey(unit) || RelativeUnit.Contains(unit);
    public static bool ValidUnit(Measurement m) => ValidUnit(m.Unit);

    public static double CalculateSize(Measurement sizeValue, Options options)
    {
        double scale;
        if (PtPerUnit.TryGetValue(sizeValue.Unit, out var pt))
        {
            scale = pt / options.FontMetrics().PtPerEm / options.SizeMultiplier;
        }
        else if (sizeValue.Unit == "mu")
        {
            scale = options.FontMetrics().CssEmPerMu;
        }
        else
        {
            Options unitOptions;
            if (options.Style.IsTight())
                unitOptions = options.HavingStyle(options.Style.Text());
            else
                unitOptions = options;

            if (sizeValue.Unit == "ex") scale = unitOptions.FontMetrics().XHeight;
            else if (sizeValue.Unit == "em") scale = unitOptions.FontMetrics().Quad;
            else throw new ParseError("Invalid unit: '" + sizeValue.Unit + "'");

            if (!ReferenceEquals(unitOptions, options))
                scale *= unitOptions.SizeMultiplier / options.SizeMultiplier;
        }
        return Math.Min(sizeValue.Number * scale, options.MaxSize);
    }

    public static string MakeEm(double n)
    {
        return JsNumber.ToFixed(n, 4) + "em";
    }
}

public static class Utils
{
    private static readonly Regex Uppercase = new("([A-Z])", RegexOptions.Compiled);

    public static string Hyphenate(string str) => Uppercase.Replace(str, "-$1").ToLowerInvariant();

    private static readonly char[] EscapeChars = { '&', '>', '<', '"', '\'' };

    public static string Escape(object text)
    {
        string s = text == null ? "undefined" : Convert.ToString(text, System.Globalization.CultureInfo.InvariantCulture);
        if (s.Length == 0 || s.IndexOfAny(EscapeChars) < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '>': sb.Append("&gt;"); break;
                case '<': sb.Append("&lt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#x27;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    public static ParseNode GetBaseElem(ParseNode group)
    {
        if (group.Type == "ordgroup")
            return group.List("body").Count == 1 ? GetBaseElem(group.List("body")[0]) : group;
        if (group.Type == "color")
            return group.List("body").Count == 1 ? GetBaseElem(group.List("body")[0]) : group;
        if (group.Type == "font")
            return GetBaseElem(group.Node("body"));
        return group;
    }

    private static readonly HashSet<string> CharacterNodeTypes = new(StringComparer.Ordinal)
        { "mathord", "textord", "atom" };

    public static bool IsCharacterBox(ParseNode group) => CharacterNodeTypes.Contains(GetBaseElem(group).Type);

    public static string ProtocolFromUrl(string url)
    {
        var m = Regex.Match(url, "^[\\x00-\\x20]*([^\\\\/#?]*?)(:|&#0*58|&#x0*3a|&colon)", RegexOptions.IgnoreCase);
        if (!m.Success) return "_relative";
        if (m.Groups[2].Value != ":") return null;
        if (!Regex.IsMatch(m.Groups[1].Value, "^[a-zA-Z][a-zA-Z0-9+\\-.]*$")) return null;
        return m.Groups[1].Value.ToLowerInvariant();
    }
}



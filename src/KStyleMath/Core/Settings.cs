namespace KStyleMath;

public sealed class Settings
{
    public bool DisplayMode = false;
    public string Output = "htmlAndMathml"; // "html", "mathml", "htmlAndMathml"
    public bool Leqno = false;
    public bool Fleqn = false;
    public bool ThrowOnError = true;
    public string ErrorColor = "#cc0000";
    public Dictionary<string, object> Macros = new(StringComparer.Ordinal);
    public double MinRuleThickness = 0;
    public bool ColorIsTextColor = false;
    public object Strict = StrictMode.Warn;
    public object Trust = false;
    public double MaxSize = double.PositiveInfinity;
    public double MaxExpand = 1000;
    public bool GlobalGroup = false;

    public Settings()
    {
    }

    public Settings Clone() => (Settings)MemberwiseClone();

    public bool IsTrusted(Dictionary<string, object> context)
    {
        if (context.TryGetValue("url", out var urlObj) && urlObj is string url && url.Length > 0 &&
            !context.ContainsKey("protocol"))
        {
            var protocol = Utils.ProtocolFromUrl(url);
            if (protocol == null) return false;
            context["protocol"] = protocol;
        }
        object trust = Trust;
        if (Trust is TrustFunction f) trust = f(context);
        return truthy(trust);
    }

    private static bool truthy(object v) => v switch
    {
        bool b => b,
        string s => s.Length > 0,
        double d => d != 0,
        null => false,
        _ => true,
    };
}

public delegate object TrustFunction(Dictionary<string, object> context);

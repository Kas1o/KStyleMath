using System.Text.Json;

namespace KStyleMath;

public static class DataLoader
{
    private static readonly Dictionary<string, JsonDocument> Cache = new();

    public static JsonDocument Load(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var doc)) return doc;
            var asm = typeof(DataLoader).Assembly;
            var stream = asm.GetManifestResourceStream("KStyleMath.Data." + name + ".json")
                         ?? throw new InvalidOperationException("Missing embedded data: " + name);
            doc = JsonDocument.Parse(stream);
            Cache[name] = doc;
            return doc;
        }
    }
}

public sealed class SymbolInfo
{
    public string Font;
    public string Group;
    public string Replace;
}

public static class Symbols
{
    public static readonly Dictionary<string, Dictionary<string, SymbolInfo>> Data = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, string> Ligatures = new(StringComparer.Ordinal);
    public static string ExtraLatin = "";

    static Symbols()
    {
        var root = DataLoader.Load("symbols").RootElement;
        foreach (var mode in new[] { "math", "text" })
        {
            var map = new Dictionary<string, SymbolInfo>(StringComparer.Ordinal);
            foreach (var prop in root.GetProperty(mode).EnumerateObject())
            {
                var v = prop.Value;
                map[prop.Name] = new SymbolInfo
                {
                    Font = v.GetProperty("font").GetString(),
                    Group = v.GetProperty("group").GetString(),
                    Replace = v.TryGetProperty("replace", out var rep) && rep.ValueKind == JsonValueKind.String
                        ? rep.GetString() : null,
                };
            }
            Data[mode] = map;
        }
        foreach (var prop in root.GetProperty("ligatures").EnumerateObject())
            Ligatures[prop.Name] = "";
        ExtraLatin = root.GetProperty("extraLatin").GetString();
    }

    public static SymbolInfo Get(string mode, string name)
        => Data.TryGetValue(mode, out var map) && map.TryGetValue(name, out var info) ? info : null;

    public static void DefineSymbol(string mode, string font, string group, string replace, string name, bool acceptUnicodeChar = false)
    {
        var info = new SymbolInfo { Font = font, Group = group, Replace = replace };
        Data[mode][name] = info;
        if (acceptUnicodeChar && !string.IsNullOrEmpty(replace))
            Data[mode][replace] = info;
    }
}

public sealed class CharacterMetrics
{
    public double Depth;
    public double Height;
    public double Italic;
    public double Skew;
    public double Width;
}

public static class FontMetricsData
{
    public static readonly Dictionary<string, Dictionary<int, double[]>> Map = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ExtraCharacterMap = new(StringComparer.Ordinal)
    {
        ["Å"] = "A", ["Ð"] = "D", ["Þ"] = "o", ["å"] = "a", ["ð"] = "d", ["þ"] = "o",
        ["А"] = "A", ["Б"] = "B", ["В"] = "B", ["Г"] = "F", ["Д"] = "A", ["Е"] = "E",
        ["Ж"] = "K", ["З"] = "3", ["И"] = "N", ["Й"] = "N", ["К"] = "K", ["Л"] = "N",
        ["М"] = "M", ["Н"] = "H", ["О"] = "O", ["П"] = "N", ["Р"] = "P", ["С"] = "C",
        ["Т"] = "T", ["У"] = "y", ["Ф"] = "O", ["Х"] = "X", ["Ц"] = "U", ["Ч"] = "h",
        ["Ш"] = "W", ["Щ"] = "W", ["Ъ"] = "B", ["Ы"] = "X", ["Ь"] = "B", ["Э"] = "3",
        ["Ю"] = "X", ["Я"] = "R",
        ["а"] = "a", ["б"] = "b", ["в"] = "a", ["г"] = "r", ["д"] = "y", ["е"] = "e",
        ["ж"] = "m", ["з"] = "e", ["и"] = "n", ["й"] = "n", ["к"] = "n", ["л"] = "n",
        ["м"] = "m", ["н"] = "n", ["о"] = "o", ["п"] = "n", ["р"] = "p", ["с"] = "c",
        ["т"] = "o", ["у"] = "y", ["ф"] = "b", ["х"] = "x", ["ц"] = "n", ["ч"] = "n",
        ["ш"] = "w", ["щ"] = "w", ["ъ"] = "a", ["ы"] = "m", ["ь"] = "a", ["э"] = "e",
        ["ю"] = "m", ["я"] = "r",
    };

    private static readonly Dictionary<string, double[]> SigmasAndXis = new(StringComparer.Ordinal)
    {
        ["slant"] = new[] { 0.250, 0.250, 0.250 },
        ["space"] = new[] { 0.000, 0.000, 0.000 },
        ["stretch"] = new[] { 0.000, 0.000, 0.000 },
        ["shrink"] = new[] { 0.000, 0.000, 0.000 },
        ["xHeight"] = new[] { 0.431, 0.431, 0.431 },
        ["quad"] = new[] { 1.000, 1.171, 1.472 },
        ["extraSpace"] = new[] { 0.000, 0.000, 0.000 },
        ["num1"] = new[] { 0.677, 0.732, 0.925 },
        ["num2"] = new[] { 0.394, 0.384, 0.387 },
        ["num3"] = new[] { 0.444, 0.471, 0.504 },
        ["denom1"] = new[] { 0.686, 0.752, 1.025 },
        ["denom2"] = new[] { 0.345, 0.344, 0.532 },
        ["sup1"] = new[] { 0.413, 0.503, 0.504 },
        ["sup2"] = new[] { 0.363, 0.431, 0.404 },
        ["sup3"] = new[] { 0.289, 0.286, 0.294 },
        ["sub1"] = new[] { 0.150, 0.143, 0.200 },
        ["sub2"] = new[] { 0.247, 0.286, 0.400 },
        ["supDrop"] = new[] { 0.386, 0.353, 0.494 },
        ["subDrop"] = new[] { 0.050, 0.071, 0.100 },
        ["delim1"] = new[] { 2.390, 1.700, 1.980 },
        ["delim2"] = new[] { 1.010, 1.157, 1.420 },
        ["axisHeight"] = new[] { 0.250, 0.250, 0.250 },
        ["defaultRuleThickness"] = new[] { 0.04, 0.049, 0.049 },
        ["bigOpSpacing1"] = new[] { 0.111, 0.111, 0.111 },
        ["bigOpSpacing2"] = new[] { 0.166, 0.166, 0.166 },
        ["bigOpSpacing3"] = new[] { 0.2, 0.2, 0.2 },
        ["bigOpSpacing4"] = new[] { 0.6, 0.611, 0.611 },
        ["bigOpSpacing5"] = new[] { 0.1, 0.143, 0.143 },
        ["sqrtRuleThickness"] = new[] { 0.04, 0.04, 0.04 },
        ["ptPerEm"] = new[] { 10.0, 10.0, 10.0 },
        ["doubleRuleSep"] = new[] { 0.2, 0.2, 0.2 },
        ["arrayRuleWidth"] = new[] { 0.04, 0.04, 0.04 },
        ["fboxsep"] = new[] { 0.3, 0.3, 0.3 },
        ["fboxrule"] = new[] { 0.04, 0.04, 0.04 },
    };

    static FontMetricsData()
    {
        var root = DataLoader.Load("fontMetricsData").RootElement;
        foreach (var font in root.EnumerateObject())
        {
            var map = new Dictionary<int, double[]>();
            foreach (var ch in font.Value.EnumerateObject())
            {
                var arr = ch.Value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                map[int.Parse(ch.Name, System.Globalization.CultureInfo.InvariantCulture)] = arr;
            }
            Map[font.Name] = map;
        }
    }

    public static CharacterMetrics GetCharacterMetrics(string character, string font, string mode)
    {
        if (!Map.TryGetValue(font, out var fontMap))
            throw new InvalidOperationException($"Font metrics not found for font: {font}.");
        int ch = character.Length > 0 ? character[0] : 0;
        if (!fontMap.TryGetValue(ch, out var metrics) && character.Length > 0 &&
            ExtraCharacterMap.TryGetValue(character[0].ToString(), out var mapped))
        {
            ch = mapped[0];
            fontMap.TryGetValue(ch, out metrics);
        }
        if (metrics == null && mode == "text" && UnicodeScripts.SupportedCodepoint(ch))
            fontMap.TryGetValue(77, out metrics);

        if (metrics != null)
            return new CharacterMetrics
            {
                Depth = metrics[0], Height = metrics[1], Italic = metrics[2],
                Skew = metrics[3], Width = metrics[4],
            };
        return null;
    }

    private static readonly Dictionary<int, FontMetrics> BySizeIndex = new();

    public static FontMetrics GetGlobalMetrics(int size)
    {
        int sizeIndex = size >= 5 ? 0 : size >= 3 ? 1 : 2;
        if (!BySizeIndex.TryGetValue(sizeIndex, out var metrics))
        {
            metrics = new FontMetrics
            {
                CssEmPerMu = SigmasAndXis["quad"][sizeIndex] / 18,
                Slant = SigmasAndXis["slant"][sizeIndex],
                Space = SigmasAndXis["space"][sizeIndex],
                Stretch = SigmasAndXis["stretch"][sizeIndex],
                Shrink = SigmasAndXis["shrink"][sizeIndex],
                XHeight = SigmasAndXis["xHeight"][sizeIndex],
                Quad = SigmasAndXis["quad"][sizeIndex],
                ExtraSpace = SigmasAndXis["extraSpace"][sizeIndex],
                Num1 = SigmasAndXis["num1"][sizeIndex],
                Num2 = SigmasAndXis["num2"][sizeIndex],
                Num3 = SigmasAndXis["num3"][sizeIndex],
                Denom1 = SigmasAndXis["denom1"][sizeIndex],
                Denom2 = SigmasAndXis["denom2"][sizeIndex],
                Sup1 = SigmasAndXis["sup1"][sizeIndex],
                Sup2 = SigmasAndXis["sup2"][sizeIndex],
                Sup3 = SigmasAndXis["sup3"][sizeIndex],
                Sub1 = SigmasAndXis["sub1"][sizeIndex],
                Sub2 = SigmasAndXis["sub2"][sizeIndex],
                SupDrop = SigmasAndXis["supDrop"][sizeIndex],
                SubDrop = SigmasAndXis["subDrop"][sizeIndex],
                Delim1 = SigmasAndXis["delim1"][sizeIndex],
                Delim2 = SigmasAndXis["delim2"][sizeIndex],
                AxisHeight = SigmasAndXis["axisHeight"][sizeIndex],
                DefaultRuleThickness = SigmasAndXis["defaultRuleThickness"][sizeIndex],
                BigOpSpacing1 = SigmasAndXis["bigOpSpacing1"][sizeIndex],
                BigOpSpacing2 = SigmasAndXis["bigOpSpacing2"][sizeIndex],
                BigOpSpacing3 = SigmasAndXis["bigOpSpacing3"][sizeIndex],
                BigOpSpacing4 = SigmasAndXis["bigOpSpacing4"][sizeIndex],
                BigOpSpacing5 = SigmasAndXis["bigOpSpacing5"][sizeIndex],
                SqrtRuleThickness = SigmasAndXis["sqrtRuleThickness"][sizeIndex],
                PtPerEm = SigmasAndXis["ptPerEm"][sizeIndex],
                DoubleRuleSep = SigmasAndXis["doubleRuleSep"][sizeIndex],
                ArrayRuleWidth = SigmasAndXis["arrayRuleWidth"][sizeIndex],
                Fboxsep = SigmasAndXis["fboxsep"][sizeIndex],
                Fboxrule = SigmasAndXis["fboxrule"][sizeIndex],
            };
            BySizeIndex[sizeIndex] = metrics;
        }
        return metrics;
    }

    public static void SetFontMetrics(string fontName, Dictionary<int, double[]> metrics)
        => Map[fontName] = metrics;
}


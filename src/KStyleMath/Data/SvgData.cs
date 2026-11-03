using System.Text.Json;

namespace KStyleMath;

public static class SvgGeometry
{
    private const double HLinePad = 80;

    private static string DoubleBrushStroke(string svgPath) => svgPath + " " + svgPath;

    private static string N(double v) => JsNumber.ToString(v);

    private static string SqrtMain(double extraVinculum, double hLinePad)
        => $"M95,{N(622 + extraVinculum + hLinePad)}\n" +
           "c-2.7,0,-7.17,-2.7,-13.5,-8c-5.8,-5.3,-9.5,-10,-9.5,-14\n" +
           "c0,-2,0.3,-3.3,1,-4c1.3,-2.7,23.83,-20.7,67.5,-54\n" +
           "c44.2,-33.3,65.8,-50.3,66.5,-51c1.3,-1.3,3,-2,5,-2c4.7,0,8.7,3.3,12,10\n" +
           "s173,378,173,378c0.7,0,35.3,-71,104,-213c68.7,-142,137.5,-285,206.5,-429\n" +
           "c69,-144,104.5,-217.7,106.5,-221\n" +
           $"l{N(extraVinculum / 2.075)} -{N(extraVinculum)}\n" +
           "c5.3,-9.3,12,-14,20,-14\n" +
           $"H400000v{N(40 + extraVinculum)}H845.2724\n" +
           "s-225.272,467,-225.272,467s-235,486,-235,486c-2.7,4.7,-9,7,-19,7\n" +
           "c-6,0,-10,-1,-12,-3s-194,-422,-194,-422s-65,47,-65,47z\n" +
           $"M{N(834 + extraVinculum)} {N(hLinePad)}h400000v{N(40 + extraVinculum)}h-400000z";

    private static string SqrtSize1(double extraVinculum, double hLinePad)
        => $"M263,{N(601 + extraVinculum + hLinePad)}c0.7,0,18,39.7,52,119\n" +
           "c34,79.3,68.167,158.7,102.5,238c34.3,79.3,51.8,119.3,52.5,120\n" +
           "c340,-704.7,510.7,-1060.3,512,-1067\n" +
           $"l{N(extraVinculum / 2.084)} -{N(extraVinculum)}\n" +
           "c4.7,-7.3,11,-11,19,-11\n" +
           $"H40000v{N(40 + extraVinculum)}H1012.3\n" +
           "s-271.3,567,-271.3,567c-38.7,80.7,-84,175,-136,283c-52,108,-89.167,185.3,-111.5,232\n" +
           "c-22.3,46.7,-33.8,70.3,-34.5,71c-4.7,4.7,-12.3,7,-23,7s-12,-1,-12,-1\n" +
           "s-109,-253,-109,-253c-72.7,-168,-109.3,-252,-110,-252c-10.7,8,-22,16.7,-34,26\n" +
           "c-22,17.3,-33.3,26,-34,26s-26,-26,-26,-26s76,-59,76,-59s76,-60,76,-60z\n" +
           $"M{N(1001 + extraVinculum)} {N(hLinePad)}h400000v{N(40 + extraVinculum)}h-400000z";

    private static string SqrtSize2(double extraVinculum, double hLinePad)
        => $"M983 {N(10 + extraVinculum + hLinePad)}\n" +
           $"l{N(extraVinculum / 3.13)} -{N(extraVinculum)}\n" +
           $"c4,-6.7,10,-10,18,-10 H400000v{N(40 + extraVinculum)}\n" +
           "H1013.1s-83.4,268,-264.1,840c-180.7,572,-277,876.3,-289,913c-4.7,4.7,-12.7,7,-24,7\n" +
           "s-12,0,-12,0c-1.3,-3.3,-3.7,-11.7,-7,-25c-35.3,-125.3,-106.7,-373.3,-214,-744\n" +
           "c-10,12,-21,25,-33,39s-32,39,-32,39c-6,-5.3,-15,-14,-27,-26s25,-30,25,-30\n" +
           "c26.7,-32.7,52,-63,76,-91s52,-60,52,-60s208,722,208,722\n" +
           "c56,-175.3,126.3,-397.3,211,-666c84.7,-268.7,153.8,-488.2,207.5,-658.5\n" +
           "c53.7,-170.3,84.5,-266.8,92.5,-289.5z\n" +
           $"M{N(1001 + extraVinculum)} {N(hLinePad)}h400000v{N(40 + extraVinculum)}h-400000z";

    private static string SqrtSize3(double extraVinculum, double hLinePad)
        => $"M424,{N(2398 + extraVinculum + hLinePad)}\n" +
           "c-1.3,-0.7,-38.5,-172,-111.5,-514c-73,-342,-109.8,-513.3,-110.5,-514\n" +
           "c0,-2,-10.7,14.3,-32,49c-4.7,7.3,-9.8,15.7,-15.5,25c-5.7,9.3,-9.8,16,-12.5,20\n" +
           "s-5,7,-5,7c-4,-3.3,-8.3,-7.7,-13,-13s-13,-13,-13,-13s76,-122,76,-122s77,-121,77,-121\n" +
           "s209,968,209,968c0,-2,84.7,-361.7,254,-1079c169.3,-717.3,254.7,-1077.7,256,-1081\n" +
           $"l{N(extraVinculum / 4.223)} -{N(extraVinculum)}c4,-6.7,10,-10,18,-10 H400000\n" +
           $"v{N(40 + extraVinculum)}H1014.6\n" +
           "s-87.3,378.7,-272.6,1166c-185.3,787.3,-279.3,1182.3,-282,1185\n" +
           "c-2,6,-10,9,-24,9\n" +
           $"c-8,0,-12,-0.7,-12,-2z M{N(1001 + extraVinculum)} {N(hLinePad)}\n" +
           $"h400000v{N(40 + extraVinculum)}h-400000z";

    private static string SqrtSize4(double extraVinculum, double hLinePad)
        => $"M473,{N(2713 + extraVinculum + hLinePad)}\n" +
           $"c339.3,-1799.3,509.3,-2700,510,-2702 l{N(extraVinculum / 5.298)} -{N(extraVinculum)}\n" +
           $"c3.3,-7.3,9.3,-11,18,-11 H400000v{N(40 + extraVinculum)}H1017.7\n" +
           "s-90.5,478,-276.2,1466c-185.7,988,-279.5,1483,-281.5,1485c-2,6,-10,9,-24,9\n" +
           "c-8,0,-12,-0.7,-12,-2c0,-1.3,-5.3,-32,-16,-92c-50.7,-293.3,-119.7,-693.3,-207,-1200\n" +
           "c0,-1.3,-5.3,8.7,-16,30c-10.7,21.3,-21.3,42.7,-32,64s-16,33,-16,33s-26,-26,-26,-26\n" +
           "s76,-153,76,-153s77,-151,77,-151c0.7,0.7,35.7,202,105,604c67.3,400.7,102,602.7,104,\n" +
           $"606zM{N(1001 + extraVinculum)} {N(hLinePad)}h400000v{N(40 + extraVinculum)}H1017.7z";

    public static string PhasePath(double y)
    {
        double x = y / 2;
        return $"M400000 {N(y)} H0 L{N(x)} 0 l65 45 L145 {N(y - 80)} H400000z";
    }

    private static string SqrtTall(double extraVinculum, double hLinePad, double viewBoxHeight)
    {
        double vertSegment = viewBoxHeight - 54 - hLinePad - extraVinculum;
        return $"M702 {N(extraVinculum + hLinePad)}H400000{N(40 + extraVinculum)}\n" +
               $"H742v{N(vertSegment)}l-4 4-4 4c-.667.7 -2 1.5-4 2.5s-4.167 1.833-6.5 2.5-5.5 1-9.5 1\n" +
               "h-12l-28-84c-16.667-52-96.667 -294.333-240-727l-212 -643 -85 170\n" +
               "c-4-3.333-8.333-7.667-13 -13l-13-13l77-155 77-156c66 199.333 139 419.667\n" +
               $"219 661 l218 661zM702 {N(hLinePad)}H400000v{N(40 + extraVinculum)}H742z";
    }

    public static string SqrtPath(string size, double extraVinculum, double viewBoxHeight)
    {
        extraVinculum = 1000 * extraVinculum;
        return size switch
        {
            "sqrtMain" => SqrtMain(extraVinculum, HLinePad),
            "sqrtSize1" => SqrtSize1(extraVinculum, HLinePad),
            "sqrtSize2" => SqrtSize2(extraVinculum, HLinePad),
            "sqrtSize3" => SqrtSize3(extraVinculum, HLinePad),
            "sqrtSize4" => SqrtSize4(extraVinculum, HLinePad),
            "sqrtTall" => SqrtTall(extraVinculum, HLinePad, viewBoxHeight),
            _ => "",
        };
    }

    public static string InnerPath(string name, double height)
    {
        string h = N(height);
        return name switch
        {
            "\u239c" => DoubleBrushStroke($"M291 0 H417 V{h} H291z"),
            "\u2223" => DoubleBrushStroke($"M145 0 H188 V{h} H145z"),
            "\u2225" => DoubleBrushStroke($"M145 0 H188 V{h} H145z") +
                        DoubleBrushStroke($"M367 0 H410 V{h} H367z"),
            "\u239f" => DoubleBrushStroke($"M457 0 H583 V{h} H457z"),
            "\u23a2" => DoubleBrushStroke($"M319 0 H403 V{h} H319z"),
            "\u23a5" => DoubleBrushStroke($"M263 0 H347 V{h} H263z"),
            "\u23aa" => DoubleBrushStroke($"M384 0 H504 V{h} H384z"),
            "\u23d0" => DoubleBrushStroke($"M312 0 H355 V{h} H312z"),
            "\u2016" => DoubleBrushStroke($"M257 0 H300 V{h} H257z") +
                        DoubleBrushStroke($"M478 0 H521 V{h} H478z"),
            _ => "",
        };
    }

    public static readonly Dictionary<string, string> Path = LoadPath();

    private static Dictionary<string, string> LoadPath()
    {
        var root = DataLoader.Load("svgGeometry").RootElement;
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject()) d[p.Name] = p.Value.GetString();
        return d;
    }

    public static string TallDelim(string label, double midHeight)
    {
        string m = N(midHeight);
        return label switch
        {
            "lbrack" => $"M403 1759 V84 H666 V0 H319 V1759 v{m} v1759 v84 h347 v-84\nH403z M403 1759 V0 H319 V1759 v{m} v1759 v84 h84z",
            "rbrack" => $"M347 1759 V0 H0 V84 H263 V1759 v{m} v1759 H0 v84 H347z\nM347 1759 V0 H263 V1759 v{m} v1759 h84z",
            "vert" => $"M145 15 v585 v{m} v585 c2.667,10,9.667,15,21,15\nc10,0,16.667,-5,20,-15 v-585 v{N(-midHeight)} v-585 c-2.667,-10,-9.667,-15,-21,-15\nc-10,0,-16.667,5,-20,15z M188 15 H145 v585 v{m} v585 h43z",
            "doublevert" => $"M145 15 v585 v{m} v585 c2.667,10,9.667,15,21,15\nc10,0,16.667,-5,20,-15 v-585 v{N(-midHeight)} v-585 c-2.667,-10,-9.667,-15,-21,-15\nc-10,0,-16.667,5,-20,15z M188 15 H145 v585 v{m} v585 h43z\nM367 15 v585 v{m} v585 c2.667,10,9.667,15,21,15\nc10,0,16.667,-5,20,-15 v-585 v{N(-midHeight)} v-585 c-2.667,-10,-9.667,-15,-21,-15\nc-10,0,-16.667,5,-20,15z M410 15 H367 v585 v{m} v585 h43z",
            "lfloor" => $"M319 602 V0 H403 V602 v{m} v1715 h263 v84 H319z\nMM319 602 V0 H403 V602 v{m} v1715 H319z",
            "rfloor" => $"M319 602 V0 H403 V602 v{m} v1799 H0 v-84 H319z\nMM319 602 V0 H403 V602 v{m} v1715 H319z",
            "lceil" => $"M403 1759 V84 H666 V0 H319 V1759 v{m} v602 h84z\nM403 1759 V0 H319 V1759 v{m} v602 h84z",
            "rceil" => $"M347 1759 V0 H0 V84 H263 V1759 v{m} v602 h84z\nM347 1759 V0 h-84 V1759 v{m} v602 h84z",
            "lparen" => "M863,9c0,-2,-2,-5,-6,-9c0,0,-17,0,-17,0c-12.7,0,-19.3,0.3,-20,1\n" +
                        "c-5.3,5.3,-10.3,11,-15,17c-242.7,294.7,-395.3,682,-458,1162c-21.3,163.3,-33.3,349,\n" +
                        $"-36,557 l0,{N(midHeight + 84)}c0.2,6,0,26,0,60c2,159.3,10,310.7,24,454c53.3,528,210,\n" +
                        "949.7,470,1265c4.7,6,9.7,11.7,15,17c0.7,0.7,7,1,19,1c0,0,18,0,18,0c4,-4,6,-7,6,-9\n" +
                        "c0,-2.7,-3.3,-8.7,-10,-18c-135.3,-192.7,-235.5,-414.3,-300.5,-665c-65,-250.7,-102.5,\n" +
                        "-544.7,-112.5,-882c-2,-104,-3,-167,-3,-189\n" +
                        $"l0,-{N(midHeight + 92)}c0,-162.7,5.7,-314,17,-454c20.7,-272,63.7,-513,129,-723c65.3,\n" +
                        "-210,155.3,-396.3,270,-559c6.7,-9.3,10,-15.3,10,-18z",
            "rparen" => "M76,0c-16.7,0,-25,3,-25,9c0,2,2,6.3,6,13c21.3,28.7,42.3,60.3,\n" +
                        "63,95c96.7,156.7,172.8,332.5,228.5,527.5c55.7,195,92.8,416.5,111.5,664.5\n" +
                        "c11.3,139.3,17,290.7,17,454c0,28,1.7,43,3.3,45l0," + N(midHeight + 9) + "\n" +
                        "c-3,4,-3.3,16.7,-3.3,38c0,162,-5.7,313.7,-17,455c-18.7,248,-55.8,469.3,-111.5,664\n" +
                        "c-55.7,194.7,-131.8,370.3,-228.5,527c-20.7,34.7,-41.7,66.3,-63,95c-2,3.3,-4,7,-6,11\n" +
                        "c0,7.3,5.7,11,17,11c0,0,11,0,11,0c9.3,0,14.3,-0.3,15,-1c5.3,-5.3,10.3,-11,15,-17\n" +
                        "c242.7,-294.7,395.3,-681.7,458,-1161c21.3,-164.7,33.3,-350.7,36,-558\n" +
                        $"l0,-{N(midHeight + 144)}c-2,-159.3,-10,-310.7,-24,-454c-53.3,-528,-210,-949.7,\n" +
                        "-470,-1265c-4.7,-6,-9.7,-11.7,-15,-17c-0.7,-0.7,-6.7,-1,-18,-1z",
            _ => throw new InvalidOperationException("Unknown stretchy delimiter."),
        };
    }
}

public static class UnicodeScripts
{
    private sealed record Script(string Name, int[][] Blocks);

    private static readonly Script[] ScriptData =
    {
        new("latin", new[] { new[] { 0x0100, 0x024f }, new[] { 0x0300, 0x036f } }),
        new("cyrillic", new[] { new[] { 0x0400, 0x04ff } }),
        new("armenian", new[] { new[] { 0x0530, 0x058F } }),
        new("brahmic", new[] { new[] { 0x0900, 0x109F } }),
        new("georgian", new[] { new[] { 0x10A0, 0x10ff } }),
        new("cjk", new[] { new[] { 0x3000, 0x30FF }, new[] { 0x4E00, 0x9FAF }, new[] { 0xFF00, 0xFF60 } }),
        new("hangul", new[] { new[] { 0xAC00, 0xD7AF } }),
    };

    private static readonly int[] AllBlocks = ScriptData.SelectMany(s => s.Blocks).SelectMany(b => b).ToArray();

    public static string ScriptFromCodepoint(int codepoint)
    {
        foreach (var script in ScriptData)
            foreach (var block in script.Blocks)
                if (codepoint >= block[0] && codepoint <= block[1])
                    return script.Name;
        return null;
    }

    public static bool SupportedCodepoint(int codepoint)
    {
        for (int i = 0; i < AllBlocks.Length; i += 2)
            if (codepoint >= AllBlocks[i] && codepoint <= AllBlocks[i + 1])
                return true;
        return false;
    }
}

public sealed class WideChar
{
    public string MathClass;
    public string TextClass;
    public string Font;
}

public static class WideCharacter
{
    public static readonly WideChar BoldUpright = new() { MathClass = "mathbf", TextClass = "textbf", Font = "Main-Bold" };
    public static readonly WideChar Italic = new() { MathClass = "mathnormal", TextClass = "textit", Font = "Math-Italic" };
    public static readonly WideChar BoldItalic = new() { MathClass = "boldsymbol", TextClass = "boldsymbol", Font = "Main-BoldItalic" };
    public static readonly WideChar Script = new() { MathClass = "mathscr", TextClass = "textscr", Font = "Script-Regular" };
    public static readonly WideChar NoFont = new() { MathClass = "", TextClass = "", Font = "" };
    public static readonly WideChar Fraktur = new() { MathClass = "mathfrak", TextClass = "textfrak", Font = "Fraktur-Regular" };
    public static readonly WideChar DoubleStruck = new() { MathClass = "mathbb", TextClass = "textbb", Font = "AMS-Regular" };
    public static readonly WideChar BoldFraktur = new() { MathClass = "mathboldfrak", TextClass = "textboldfrak", Font = "Fraktur-Regular" };
    public static readonly WideChar SansSerif = new() { MathClass = "mathsf", TextClass = "textsf", Font = "SansSerif-Regular" };
    public static readonly WideChar BoldSansSerif = new() { MathClass = "mathboldsf", TextClass = "textboldsf", Font = "SansSerif-Bold" };
    public static readonly WideChar ItalicSansSerif = new() { MathClass = "mathitsf", TextClass = "textitsf", Font = "SansSerif-Italic" };
    public static readonly WideChar Monospace = new() { MathClass = "mathtt", TextClass = "texttt", Font = "Typewriter-Regular" };

    private static readonly WideChar[] WideLatinLetterData =
    {
        BoldUpright, BoldUpright, Italic, Italic, BoldItalic, BoldItalic,
        Script, NoFont, NoFont, NoFont, Fraktur, Fraktur, DoubleStruck, DoubleStruck,
        BoldFraktur, BoldFraktur, SansSerif, SansSerif, BoldSansSerif, BoldSansSerif,
        ItalicSansSerif, ItalicSansSerif, NoFont, NoFont, Monospace, Monospace,
    };

    private static readonly WideChar[] WideNumeralData =
    {
        BoldUpright, NoFont, SansSerif, BoldSansSerif, Monospace,
    };

    public static WideChar WideCharacterFont(string wideChar)
    {
        int H = wideChar.Length > 0 ? wideChar[0] : 0;
        int L = wideChar.Length > 1 ? wideChar[1] : 0;
        int codePoint = (H - 0xD800) * 0x400 + (L - 0xDC00) + 0x10000;

        if (codePoint >= 0x1D400 && codePoint < 0x1D6A4)
        {
            int i = (codePoint - 0x1D400) / 26;
            return WideLatinLetterData[i];
        }
        if (codePoint >= 0x1D7CE && codePoint <= 0x1D7FF)
        {
            int i = (codePoint - 0x1D7CE) / 10;
            return WideNumeralData[i];
        }
        if (codePoint == 0x1D6A5 || codePoint == 0x1D6A6) return WideLatinLetterData[0];
        if (codePoint > 0x1D6A6 && codePoint < 0x1D7CE) return NoFont;
        throw new ParseError("Unsupported character: " + wideChar);
    }
}

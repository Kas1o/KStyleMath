namespace KStyleMath;

public sealed class VListElem
{
    public string Type = "elem";
    public HtmlDomNode Elem;
    public double Shift;
    public string MarginLeft;
    public string MarginRight;
    public List<string> WrapperClasses;
    public CssStyle WrapperStyle;
}

public sealed class VListKern
{
    public string Type = "kern";
    public double Size;
}

public sealed class VListParam
{
    public string PositionType;
    public double PositionData;
    public List<object> Children;

    public static VListParam IndividualShift(List<VListElem> children)
        => new() { PositionType = "individualShift", Children = children.Cast<object>().ToList() };

    public static VListParam Top(double positionData, List<object> children)
        => new() { PositionType = "top", PositionData = positionData, Children = children };

    public static VListParam Bottom(double positionData, List<object> children)
        => new() { PositionType = "bottom", PositionData = positionData, Children = children };

    public static VListParam Shift(double positionData, List<object> children)
        => new() { PositionType = "shift", PositionData = positionData, Children = children };

    public static VListParam FirstBaseline(List<object> children)
        => new() { PositionType = "firstBaseline", Children = children };
}

public static class BuildCommon
{
    public sealed class FontMapEntry
    {
        public string Variant;
        public string FontName;
    }

    public static readonly Dictionary<string, FontMapEntry> FontMap = new(StringComparer.Ordinal)
    {
        ["mathbf"] = new() { Variant = "bold", FontName = "Main-Bold" },
        ["mathrm"] = new() { Variant = "normal", FontName = "Main-Regular" },
        ["textit"] = new() { Variant = "italic", FontName = "Main-Italic" },
        ["mathit"] = new() { Variant = "italic", FontName = "Main-Italic" },
        ["mathnormal"] = new() { Variant = "italic", FontName = "Math-Italic" },
        ["mathsfit"] = new() { Variant = "sans-serif-italic", FontName = "SansSerif-Italic" },
        ["mathbb"] = new() { Variant = "double-struck", FontName = "AMS-Regular" },
        ["mathcal"] = new() { Variant = "script", FontName = "Caligraphic-Regular" },
        ["mathfrak"] = new() { Variant = "fraktur", FontName = "Fraktur-Regular" },
        ["mathscr"] = new() { Variant = "script", FontName = "Script-Regular" },
        ["mathsf"] = new() { Variant = "sans-serif", FontName = "SansSerif-Regular" },
        ["mathtt"] = new() { Variant = "monospace", FontName = "Typewriter-Regular" },
    };

    public static readonly Dictionary<string, (string Path, double Width, double Height)> SvgData = new()
    {
        ["vec"] = ("vec", 0.471, 0.714),
        ["oiintSize1"] = ("oiintSize1", 0.957, 0.499),
        ["oiintSize2"] = ("oiintSize2", 1.472, 0.659),
        ["oiiintSize1"] = ("oiiintSize1", 1.304, 0.499),
        ["oiiintSize2"] = ("oiiintSize2", 1.98, 0.659),
    };

    private static (string Value, CharacterMetrics Metrics) LookupSymbol(string value, string fontName, string mode)
    {
        var info = Symbols.Get(mode, value);
        if (info?.Replace != null) value = info.Replace;
        return (value, FontMetricsData.GetCharacterMetrics(value, fontName, mode));
    }

    public static SymbolNode MakeSymbol(string value, string fontName, string mode, Options options, List<string> classes = null)
    {
        var lookup = LookupSymbol(value, fontName, mode);
        var metrics = lookup.Metrics;
        value = lookup.Value;
        SymbolNode symbolNode;
        if (metrics != null)
        {
            double italic = metrics.Italic;
            if (mode == "text" || options?.Font == "mathit") italic = 0;
            symbolNode = new SymbolNode(value, metrics.Height, metrics.Depth, italic, metrics.Skew,
                metrics.Width, classes);
        }
        else
        {
            StrictHelper.Handle(options.Strict, "symbolNotInFont",
                $"No character metrics for '{value}' in style '{fontName}' and mode '{mode}'", null, true);
            symbolNode = new SymbolNode(value, 0, 0, 0, 0, 0, classes);
        }
        symbolNode.MaxFontSize = options.SizeMultiplier;
        if (options.Style.IsTight()) symbolNode.Classes.Add("mtight");
        var color = options.GetColor();
        if (color != null) symbolNode.Style.Color = color;
        return symbolNode;
    }

    public static SymbolNode Mathsym(string value, string mode, Options options, List<string> classes = null)
    {
        classes ??= new List<string>();
        if (options.Font == "boldsymbol" && LookupSymbol(value, "Main-Bold", mode).Metrics != null)
            return MakeSymbol(value, "Main-Bold", mode, options, classes.Concat(new[] { "mathbf" }).ToList());
        var info = Symbols.Get(mode, value);
        if (value == "\\" || info?.Font == "main")
            return MakeSymbol(value, "Main-Regular", mode, options, classes);
        return MakeSymbol(value, "AMS-Regular", mode, options, classes.Concat(new[] { "amsrm" }).ToList());
    }

    private static (string FontName, string FontClass) BoldSymbol(string value, string mode, string type)
    {
        if (type != "textord" && LookupSymbol(value, "Math-BoldItalic", mode).Metrics != null)
            return ("Math-BoldItalic", "boldsymbol");
        return ("Main-Bold", "mathbf");
    }

    public static HtmlDomNode MakeOrd(ParseNode group, Options options)
    {
        string type = group.Type == "mathord" ? "mathord" : "textord";
        string mode = group.Mode;
        string text = group.Str("text");
        var classes = new List<string> { "mord" };
        var font = options.Font;
        var fontFamily = options.FontFamily;
        var fontWeight = options.FontWeight;
        var fontShape = options.FontShape;

        bool useFont = mode == "math" || (mode == "text" && !string.IsNullOrEmpty(font));
        string fontOrFamily = useFont ? font : fontFamily;
        string wideFontName = "";
        string wideFontClass = "";
        if (text.Length > 0 && text[0] == 0xD835)
        {
            var wideCharData = WideCharacter.WideCharacterFont(text);
            wideFontName = wideCharData.Font;
            wideFontClass = mode == "math" ? wideCharData.MathClass : wideCharData.TextClass;
        }
        if (!string.IsNullOrEmpty(wideFontName))
            return MakeSymbol(text, wideFontName, mode, options, classes.Concat(new[] { wideFontClass }).ToList());
        if (!string.IsNullOrEmpty(fontOrFamily))
        {
            string fontName;
            List<string> fontClasses;
            if (fontOrFamily == "boldsymbol")
            {
                var fontData = BoldSymbol(text, mode, type);
                fontName = fontData.FontName;
                fontClasses = new List<string> { fontData.FontClass };
            }
            else if (useFont)
            {
                fontName = FontMap[font].FontName;
                fontClasses = new List<string> { font };
            }
            else
            {
                fontName = RetrieveTextFontName(fontFamily, fontWeight, fontShape);
                fontClasses = new List<string> { fontFamily, fontWeight, fontShape };
            }

            if (LookupSymbol(text, fontName, mode).Metrics != null)
                return MakeSymbol(text, fontName, mode, options, classes.Concat(fontClasses).ToList());
            if (Symbols.Ligatures.ContainsKey(text) && fontName.StartsWith("Typewriter", StringComparison.Ordinal))
            {
                var parts = new List<VirtualNode>();
                for (int i = 0; i < text.Length; i++)
                    parts.Add(MakeSymbol(text[i].ToString(), fontName, mode, options, classes.Concat(fontClasses).ToList()));
                return MakeFragment(parts);
            }
        }

        if (type == "mathord")
            return MakeSymbol(text, "Math-Italic", mode, options, classes.Concat(new[] { "mathnormal" }).ToList());
        if (type == "textord")
        {
            var info = Symbols.Get(mode, text);
            var fontName = info?.Font;
            if (fontName == "ams")
            {
                var name = RetrieveTextFontName("amsrm", fontWeight, fontShape);
                return MakeSymbol(text, name, mode, options,
                    classes.Concat(new[] { "amsrm", fontWeight, fontShape }).ToList());
            }
            if (fontName == "main" || fontName == null)
            {
                var name = RetrieveTextFontName("textrm", fontWeight, fontShape);
                return MakeSymbol(text, name, mode, options,
                    classes.Concat(new[] { fontWeight, fontShape }).ToList());
            }
            var fontName2 = RetrieveTextFontName(fontName, fontWeight, fontShape);
            return MakeSymbol(text, fontName2, mode, options,
                classes.Concat(new[] { fontName2, fontWeight, fontShape }).ToList());
        }
        throw new InvalidOperationException("unexpected type: " + type + " in makeOrd");
    }

    private static bool CanCombine(SymbolNode prev, SymbolNode next)
    {
        if (DomTreeUtil.CreateClass(prev.Classes) != DomTreeUtil.CreateClass(next.Classes)
            || prev.Skew != next.Skew
            || prev.MaxFontSize != next.MaxFontSize
            || (prev.Italic != 0 && prev.HasClass("mathnormal")))
            return false;

        if (prev.Classes.Count == 1)
        {
            var cls = prev.Classes[0];
            if (cls == "mbin" || cls == "mord") return false;
        }

        foreach (var key in prev.Style.Keys)
            if (prev.Style[key] != next.Style[key]) return false;
        foreach (var key in next.Style.Keys)
            if (prev.Style[key] != next.Style[key]) return false;
        return true;
    }

    public static List<VirtualNode> TryCombineChars(List<VirtualNode> chars)
    {
        for (int i = 0; i < chars.Count - 1; i++)
        {
            if (chars[i] is SymbolNode prev && chars[i + 1] is SymbolNode next && CanCombine(prev, next))
            {
                prev.Text += next.Text;
                prev.Height = Math.Max(prev.Height, next.Height);
                prev.Depth = Math.Max(prev.Depth, next.Depth);
                prev.Italic = next.Italic;
                chars.RemoveAt(i + 1);
                i--;
            }
        }
        return chars;
    }

    private static void SizeElementFromChildren(HtmlDomNode elem, List<VirtualNode> children)
    {
        double height = 0, depth = 0, maxFontSize = 0;
        foreach (var child in children)
        {
            var h = child as HtmlDomNode;
            if (h == null) continue;
            if (h.Height > height) height = h.Height;
            if (h.Depth > depth) depth = h.Depth;
            if (h.MaxFontSize > maxFontSize) maxFontSize = h.MaxFontSize;
        }
        elem.Height = height;
        elem.Depth = depth;
        elem.MaxFontSize = maxFontSize;
    }

    public static Span MakeSpan(List<string> classes = null, List<VirtualNode> children = null,
        Options options = null, CssStyle style = null)
    {
        var span = new Span(classes, children, options, style);
        SizeElementFromChildren(span, span.Children);
        return span;
    }

    public static Span MakeSpan(List<string> classes, List<HtmlDomNode> children, Options options = null, CssStyle style = null)
        => MakeSpan(classes, children.Cast<VirtualNode>().ToList(), options, style);

    public static Span MakeSvgSpan(List<string> classes = null, List<VirtualNode> children = null,
        Options options = null, CssStyle style = null)
        => new Span(classes, children, options, style);

    public static Span MakeLineSpan(string className, Options options, double? thickness = null)
    {
        var line = MakeSpan(new List<string> { className }, new List<VirtualNode>(), options);
        line.Height = Math.Max(thickness ?? options.FontMetrics().DefaultRuleThickness, options.MinRuleThickness);
        line.Style.BorderBottomWidth = Units.MakeEm(line.Height);
        line.MaxFontSize = 1.0;
        return line;
    }

    public static Anchor MakeAnchor(string href, List<string> classes, List<HtmlDomNode> children, Options options)
        => MakeAnchor(href, classes, children.Cast<VirtualNode>().ToList(), options);

    public static Anchor MakeAnchor(string href, List<string> classes, List<VirtualNode> children, Options options)
    {
        var anchor = new Anchor(href, classes, children, options);
        SizeElementFromChildren(anchor, anchor.Children);
        return anchor;
    }

    public static DocumentFragment MakeFragment(List<HtmlDomNode> children)
        => MakeFragment(children.Cast<VirtualNode>().ToList());

    public static DocumentFragment MakeFragment(List<VirtualNode> children)
    {
        var fragment = new DocumentFragment(children);
        SizeElementFromChildren(fragment, children);
        return fragment;
    }

    public static HtmlDomNode WrapFragment(HtmlDomNode group, Options options)
        => group is DocumentFragment ? MakeSpan(new List<string>(), new List<VirtualNode> { group }, options) : group;

    private static (List<object> Children, double Depth) GetVListChildrenAndDepth(VListParam p)
    {
        if (p.PositionType == "individualShift")
        {
            var oldChildren = p.Children.Cast<VListElem>().ToList();
            var children = new List<object> { oldChildren[0] };
            double depth = -oldChildren[0].Shift - oldChildren[0].Elem.Depth;
            double currPos = depth;
            for (int i = 1; i < oldChildren.Count; i++)
            {
                double diff = -oldChildren[i].Shift - currPos - oldChildren[i].Elem.Depth;
                double size = diff - (oldChildren[i - 1].Elem.Height + oldChildren[i - 1].Elem.Depth);
                currPos += diff;
                children.Add(new VListKern { Size = size });
                children.Add(oldChildren[i]);
            }
            return (children, depth);
        }

        double d;
        if (p.PositionType == "top")
        {
            double bottom = p.PositionData;
            foreach (var child in p.Children)
                bottom -= child is VListKern k ? k.Size : ((VListElem)child).Elem.Height + ((VListElem)child).Elem.Depth;
            d = bottom;
        }
        else if (p.PositionType == "bottom") d = -p.PositionData;
        else
        {
            var firstChild = (VListElem)p.Children[0];
            if (p.PositionType == "shift") d = -firstChild.Elem.Depth - p.PositionData;
            else if (p.PositionType == "firstBaseline") d = -firstChild.Elem.Depth;
            else throw new InvalidOperationException($"Invalid positionType {p.PositionType}.");
        }
        return (p.Children, d);
    }

    public static Span MakeVList(VListParam vparams, Options options)
    {
        var (children, depth) = GetVListChildrenAndDepth(vparams);
        double pstrutSize = 0;
        foreach (var child in children)
            if (child is VListElem e)
                pstrutSize = Math.Max(pstrutSize, Math.Max(e.Elem.MaxFontSize, e.Elem.Height));
        pstrutSize += 2;
        var pstrut = MakeSpan(new List<string> { "pstrut" }, new List<VirtualNode>());
        pstrut.Style.Height = Units.MakeEm(pstrutSize);

        var realChildren = new List<VirtualNode>();
        double minPos = depth, maxPos = depth, currPos = depth;
        foreach (var child in children)
        {
            if (child is VListKern kern)
            {
                currPos += kern.Size;
            }
            else
            {
                var elem = (VListElem)child;
                var classes = elem.WrapperClasses ?? new List<string>();
                var style = elem.WrapperStyle ?? new CssStyle();
                var childWrap = MakeSpan(classes, new List<VirtualNode> { pstrut, elem.Elem }, null, style);
                childWrap.Style.Top = Units.MakeEm(-pstrutSize - currPos - elem.Elem.Depth);
                if (elem.MarginLeft != null) childWrap.Style.MarginLeft = elem.MarginLeft;
                if (elem.MarginRight != null) childWrap.Style.MarginRight = elem.MarginRight;
                realChildren.Add(childWrap);
                currPos += elem.Elem.Height + elem.Elem.Depth;
            }
            minPos = Math.Min(minPos, currPos);
            maxPos = Math.Max(maxPos, currPos);
        }

        var vlist = MakeSpan(new List<string> { "vlist" }, realChildren);
        vlist.Style.Height = Units.MakeEm(maxPos);

        List<VirtualNode> rows;
        if (minPos < 0)
        {
            var emptySpan = MakeSpan(new List<string>(), new List<VirtualNode>());
            var depthStrut = MakeSpan(new List<string> { "vlist" }, new List<VirtualNode> { emptySpan });
            depthStrut.Style.Height = Units.MakeEm(-minPos);
            var topStrut = MakeSpan(new List<string> { "vlist-s" },
                new List<VirtualNode> { new SymbolNode("\u200b") });
            rows = new List<VirtualNode>
            {
                MakeSpan(new List<string> { "vlist-r" }, new List<VirtualNode> { vlist, topStrut }),
                MakeSpan(new List<string> { "vlist-r" }, new List<VirtualNode> { depthStrut }),
            };
        }
        else
        {
            rows = new List<VirtualNode>
            {
                MakeSpan(new List<string> { "vlist-r" }, new List<VirtualNode> { vlist }),
            };
        }

        var vtable = MakeSpan(new List<string> { "vlist-t" }, rows);
        if (rows.Count == 2) vtable.Classes.Add("vlist-t2");
        vtable.Height = maxPos;
        vtable.Depth = -minPos;
        return vtable;
    }

    public static Span MakeGlue(Measurement measurement, Options options)
    {
        var rule = MakeSpan(new List<string> { "mspace" }, new List<VirtualNode>(), options);
        double size = Units.CalculateSize(measurement, options);
        rule.Style.MarginRight = Units.MakeEm(size);
        return rule;
    }

    public static string RetrieveTextFontName(string fontFamily, string fontWeight, string fontShape)
    {
        string baseFontName;
        string fontStylesName;
        switch (fontFamily)
        {
            case "amsrm": baseFontName = "AMS"; break;
            case "textrm": baseFontName = "Main"; break;
            case "textsf": baseFontName = "SansSerif"; break;
            case "texttt": baseFontName = "Typewriter"; break;
            default: baseFontName = fontFamily; break;
        }
        if (fontWeight == "textbf" && fontShape == "textit") fontStylesName = "BoldItalic";
        else if (fontWeight == "textbf") fontStylesName = "Bold";
        else if (fontShape == "textit") fontStylesName = "Italic";
        else fontStylesName = "Regular";
        return baseFontName + "-" + fontStylesName;
    }

    public static Span StaticSvg(string value, Options options)
    {
        var (pathName, width, height) = SvgData[value];
        var path = new PathNode(pathName);
        var svgNode = new SvgNode(new List<VirtualNode> { path }, new Dictionary<string, string>
        {
            ["width"] = Units.MakeEm(width),
            ["height"] = Units.MakeEm(height),
            ["style"] = "width:" + Units.MakeEm(width),
            ["viewBox"] = "0 0 " + JsNumber.ToString(1000 * width) + " " + JsNumber.ToString(1000 * height),
            ["preserveAspectRatio"] = "xMinYMin",
        });
        var span = MakeSvgSpan(new List<string> { "katex-overlay" }, new List<VirtualNode> { svgNode }, options);
        span.Height = height;
        span.Style.Height = Units.MakeEm(height);
        span.Style.Width = Units.MakeEm(width);
        return span;
    }
}



namespace KStyleMath;

/// <summary>
/// This file deals with creating delimiters of various sizes. The TeXbook
/// discusses these routines on page 441-442, in the "Another subroutine sets box
/// x to a specified variable delimiter" paragraph.
///
/// There are three main routines here. `MakeSmallDelim` makes a delimiter in the
/// normal font, but in either text, script, or scriptscript style.
/// `MakeLargeDelim` makes a delimiter in textstyle, but in one of the Size1,
/// Size2, Size3, or Size4 fonts. `MakeStackedDelim` makes a delimiter out of
/// smaller pieces that are stacked on top of one another.
///
/// The functions take a parameter `center`, which determines if the delimiter
/// should be centered around the axis.
///
/// Then, there are three exposed functions. `MakeSizedDelim` makes a delimiter
/// in one of the given sizes. This is used for things like `\bigl`.
/// `MakeCustomSizedDelim` makes a delimiter with a given total height+depth. It
/// is called in places like `\sqrt`. `MakeLeftRightDelim` makes an appropriate
/// delimiter which surrounds an expression of a given height an depth. It is
/// used in `\left` and `\right`.
/// </summary>
public static class Delimiter
{
    /// <summary>Result of <see cref="MakeSqrtImage"/>.</summary>
    public sealed class SqrtImage
    {
        public Span Span;
        public double RuleWidth;
        public double AdvanceWidth;

        public void Deconstruct(out Span span, out double ruleWidth, out double advanceWidth)
        {
            span = Span;
            ruleWidth = RuleWidth;
            advanceWidth = AdvanceWidth;
        }
    }

    private sealed class DelimiterSpec
    {
        public string Type;
        public Style Style;
        public int Size;
    }

    private static double Round(double value)
        => Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Get the metrics for a given symbol and font, after transformation (i.e.
    /// after following replacement from symbols.js)
    /// </summary>
    private static CharacterMetrics GetMetrics(
        string symbol,
        string font,
        string mode)
    {
        var info = Symbols.Get("math", symbol);
        var replace = info?.Replace;
        var metrics =
            FontMetricsData.GetCharacterMetrics(replace ?? symbol, font, mode);
        if (metrics == null)
        {
            throw new Exception($"Unsupported symbol {symbol} and font size {font}.");
        }
        return metrics;
    }

    /// <summary>
    /// Puts a delimiter span in a given style, and adds appropriate height, depth,
    /// and maxFontSizes.
    /// </summary>
    private static Span StyleWrap(
        HtmlDomNode delim,
        Style toStyle,
        Options options,
        List<string> classes)
    {
        var newOptions = options.HavingBaseStyle(toStyle);

        var span = BuildCommon.MakeSpan(
            classes.Concat(newOptions.SizingClasses(options)).ToList(),
            new List<VirtualNode> { delim }, options);

        double delimSizeMultiplier =
            newOptions.SizeMultiplier / options.SizeMultiplier;
        span.Height *= delimSizeMultiplier;
        span.Depth *= delimSizeMultiplier;
        span.MaxFontSize = newOptions.SizeMultiplier;

        return span;
    }

    private static void CenterSpan(
        Span span,
        Options options,
        Style style)
    {
        var newOptions = options.HavingBaseStyle(style);
        double shift =
            (1 - options.SizeMultiplier / newOptions.SizeMultiplier) *
            options.FontMetrics().AxisHeight;

        span.Classes.Add("delimcenter");
        span.Style.Top = Units.MakeEm(shift);
        span.Height -= shift;
        span.Depth += shift;
    }

    /// <summary>
    /// Makes a small delimiter. This is a delimiter that comes in the Main-Regular
    /// font, but is restyled to either be in textstyle, scriptstyle, or
    /// scriptscriptstyle.
    /// </summary>
    private static Span MakeSmallDelim(
        string delim,
        Style style,
        bool center,
        Options options,
        string mode,
        List<string> classes)
    {
        var text = BuildCommon.MakeSymbol(delim, "Main-Regular", mode, options);
        var span = StyleWrap(text, style, options, classes);
        if (center)
        {
            CenterSpan(span, options, style);
        }
        return span;
    }

    /// <summary>
    /// Builds a symbol in the given font size (note size is an integer)
    /// </summary>
    private static SymbolNode MathrmSize(
        string value,
        int size,
        string mode,
        Options options)
    {
        return BuildCommon.MakeSymbol(value, $"Size{size}-Regular", mode, options);
    }

    /// <summary>
    /// Makes a large delimiter. This is a delimiter that comes in the Size1, Size2,
    /// Size3, or Size4 fonts. It is always rendered in textstyle.
    /// </summary>
    private static Span MakeLargeDelim(
        string delim,
        int size,
        bool center,
        Options options,
        string mode,
        List<string> classes)
    {
        var inner = MathrmSize(delim, size, mode, options);
        var span = StyleWrap(
            BuildCommon.MakeSpan(new List<string> { "delimsizing", "size" + JsNumber.ToString(size) },
                new List<VirtualNode> { inner }, options),
            Style.TEXT, options, classes);
        if (center)
        {
            CenterSpan(span, options, Style.TEXT);
        }
        return span;
    }

    /// <summary>
    /// Make a span from a font glyph with the given offset and in the given font.
    /// This is used in MakeStackedDelim to make the stacking pieces for the delimiter.
    /// </summary>
    private static VListElem MakeGlyphSpan(
        string symbol,
        string font,
        string mode,
        Options options)
    {
        string sizeClass;
        // Apply the correct CSS class to choose the right font.
        if (font == "Size1-Regular")
        {
            sizeClass = "delim-size1";
        }
        else /* if (font === "Size4-Regular") */
        {
            sizeClass = "delim-size4";
        }

        var corner = BuildCommon.MakeSpan(
            new List<string> { "delimsizinginner", sizeClass },
            new List<VirtualNode>
            {
                BuildCommon.MakeSpan(new List<string>(),
                    new List<VirtualNode> { BuildCommon.MakeSymbol(symbol, font, mode, options) }),
            });

        // Since this will be passed into `makeVList` in the end, wrap the element
        // in the appropriate tag that VList uses.
        return new VListElem { Type = "elem", Elem = corner };
    }

    private static VListElem MakeInner(
        string ch,
        double height,
        Options options)
    {
        // Create a span with inline SVG for the inner part of a tall stacked delimiter.
        double width;
        if (FontMetricsData.Map.TryGetValue("Size4-Regular", out var size4)
            && size4.TryGetValue(ch.Length > 0 ? ch[0] : 0, out var metrics4))
        {
            width = metrics4[4];
        }
        else
        {
            width = FontMetricsData.Map["Size1-Regular"][ch.Length > 0 ? ch[0] : 0][4];
        }
        var path = new PathNode("inner", SvgGeometry.InnerPath(ch, Round(1000 * height)));
        var svgNode = new SvgNode(new List<VirtualNode> { path }, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["width"] = Units.MakeEm(width),
            ["height"] = Units.MakeEm(height),
            // Override CSS rule `.katex svg { width: 100% }`
            ["style"] = "width:" + Units.MakeEm(width),
            ["viewBox"] = "0 0 " + JsNumber.ToString(1000 * width) + " " + JsNumber.ToString(Round(1000 * height)),
            ["preserveAspectRatio"] = "xMinYMin",
        });
        var span = BuildCommon.MakeSvgSpan(new List<string>(), new List<VirtualNode> { svgNode }, options);
        span.Height = height;
        span.Style.Height = Units.MakeEm(height);
        span.Style.Width = Units.MakeEm(width);
        return new VListElem { Type = "elem", Elem = span };
    }

    // Helpers for makeStackedDelim
    private const double LapInEms = 0.008;
    private static readonly HashSet<string> Verts = new(StringComparer.Ordinal)
        { "|", "\\lvert", "\\rvert", "\\vert" };
    private static readonly HashSet<string> DoubleVerts = new(StringComparer.Ordinal)
        { "\\|", "\\lVert", "\\rVert", "\\Vert" };

    /// <summary>
    /// Make a stacked delimiter out of a given delimiter, with the total height at
    /// least `heightTotal`. This routine is mentioned on page 442 of the TeXbook.
    /// </summary>
    private static Span MakeStackedDelim(
        string delim,
        double heightTotal,
        bool center,
        Options options,
        string mode,
        List<string> classes)
    {
        // There are four parts, the top, an optional middle, a repeated part, and a
        // bottom.
        string top;
        string middle;
        string repeat;
        string bottom;
        string svgLabel = "";
        int viewBoxWidth = 0;
        top = repeat = bottom = delim;
        middle = null;
        // Also keep track of what font the delimiters are in
        string font = "Size1-Regular";

        // We set the parts and font based on the symbol. Note that we use
        // '\u23d0' instead of '|' and '\u2016' instead of '\\|' for the
        // repeats of the arrows
        if (delim == "\\uparrow")
        {
            repeat = bottom = "\u23d0";
        }
        else if (delim == "\\Uparrow")
        {
            repeat = bottom = "\u2016";
        }
        else if (delim == "\\downarrow")
        {
            top = repeat = "\u23d0";
        }
        else if (delim == "\\Downarrow")
        {
            top = repeat = "\u2016";
        }
        else if (delim == "\\updownarrow")
        {
            top = "\\uparrow";
            repeat = "\u23d0";
            bottom = "\\downarrow";
        }
        else if (delim == "\\Updownarrow")
        {
            top = "\\Uparrow";
            repeat = "\u2016";
            bottom = "\\Downarrow";
        }
        else if (Verts.Contains(delim))
        {
            repeat = "\u2223";
            svgLabel = "vert";
            viewBoxWidth = 333;
        }
        else if (DoubleVerts.Contains(delim))
        {
            repeat = "\u2225";
            svgLabel = "doublevert";
            viewBoxWidth = 556;
        }
        else if (delim == "[" || delim == "\\lbrack")
        {
            top = "\u23a1";
            repeat = "\u23a2";
            bottom = "\u23a3";
            font = "Size4-Regular";
            svgLabel = "lbrack";
            viewBoxWidth = 667;
        }
        else if (delim == "]" || delim == "\\rbrack")
        {
            top = "\u23a4";
            repeat = "\u23a5";
            bottom = "\u23a6";
            font = "Size4-Regular";
            svgLabel = "rbrack";
            viewBoxWidth = 667;
        }
        else if (delim == "\\lfloor" || delim == "\u230a")
        {
            repeat = top = "\u23a2";
            bottom = "\u23a3";
            font = "Size4-Regular";
            svgLabel = "lfloor";
            viewBoxWidth = 667;
        }
        else if (delim == "\\lceil" || delim == "\u2308")
        {
            top = "\u23a1";
            repeat = bottom = "\u23a2";
            font = "Size4-Regular";
            svgLabel = "lceil";
            viewBoxWidth = 667;
        }
        else if (delim == "\\rfloor" || delim == "\u230b")
        {
            repeat = top = "\u23a5";
            bottom = "\u23a6";
            font = "Size4-Regular";
            svgLabel = "rfloor";
            viewBoxWidth = 667;
        }
        else if (delim == "\\rceil" || delim == "\u2309")
        {
            top = "\u23a4";
            repeat = bottom = "\u23a5";
            font = "Size4-Regular";
            svgLabel = "rceil";
            viewBoxWidth = 667;
        }
        else if (delim == "(" || delim == "\\lparen")
        {
            top = "\u239b";
            repeat = "\u239c";
            bottom = "\u239d";
            font = "Size4-Regular";
            svgLabel = "lparen";
            viewBoxWidth = 875;
        }
        else if (delim == ")" || delim == "\\rparen")
        {
            top = "\u239e";
            repeat = "\u239f";
            bottom = "\u23a0";
            font = "Size4-Regular";
            svgLabel = "rparen";
            viewBoxWidth = 875;
        }
        else if (delim == "\\{" || delim == "\\lbrace")
        {
            top = "\u23a7";
            middle = "\u23a8";
            bottom = "\u23a9";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }
        else if (delim == "\\}" || delim == "\\rbrace")
        {
            top = "\u23ab";
            middle = "\u23ac";
            bottom = "\u23ad";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }
        else if (delim == "\\lgroup" || delim == "\u27ee")
        {
            top = "\u23a7";
            bottom = "\u23a9";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }
        else if (delim == "\\rgroup" || delim == "\u27ef")
        {
            top = "\u23ab";
            bottom = "\u23ad";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }
        else if (delim == "\\lmoustache" || delim == "\u23b0")
        {
            top = "\u23a7";
            bottom = "\u23ad";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }
        else if (delim == "\\rmoustache" || delim == "\u23b1")
        {
            top = "\u23ab";
            bottom = "\u23a9";
            repeat = "\u23aa";
            font = "Size4-Regular";
        }

        // Get the metrics of the four sections
        var topMetrics = GetMetrics(top, font, mode);
        double topHeightTotal = topMetrics.Height + topMetrics.Depth;
        var repeatMetrics = GetMetrics(repeat, font, mode);
        double repeatHeightTotal = repeatMetrics.Height + repeatMetrics.Depth;
        var bottomMetrics = GetMetrics(bottom, font, mode);
        double bottomHeightTotal = bottomMetrics.Height + bottomMetrics.Depth;
        double middleHeightTotal = 0;
        int middleFactor = 1;
        if (middle != null)
        {
            var middleMetrics = GetMetrics(middle, font, mode);
            middleHeightTotal = middleMetrics.Height + middleMetrics.Depth;
            middleFactor = 2; // repeat symmetrically above and below middle
        }

        // Calculate the minimal height that the delimiter can have.
        // It is at least the size of the top, bottom, and optional middle combined.
        double minHeight = topHeightTotal + bottomHeightTotal + middleHeightTotal;

        // Compute the number of copies of the repeat symbol we will need
        double repeatCount = Math.Max(0, Math.Ceiling(
            (heightTotal - minHeight) / (middleFactor * repeatHeightTotal)));

        // Compute the total height of the delimiter including all the symbols
        double realHeightTotal =
            minHeight + repeatCount * middleFactor * repeatHeightTotal;

        // The center of the delimiter is placed at the center of the axis. Note
        // that in this context, "center" means that the delimiter should be
        // centered around the axis in the current style, while normally it is
        // centered around the axis in textstyle.
        double axisHeight = options.FontMetrics().AxisHeight;
        if (center)
        {
            axisHeight *= options.SizeMultiplier;
        }
        // Calculate the depth
        double depth = realHeightTotal / 2 - axisHeight;

        // Now, we start building the pieces that will go into the vlist
        // Keep a list of the pieces of the stacked delimiter
        var stack = new List<object>();
        var lap = new VListKern { Type = "kern", Size = -1 * LapInEms };

        if (svgLabel.Length > 0)
        {
            // Instead of stacking glyphs, create a single SVG.
            // This evades browser problems with imprecise positioning of spans.
            double midHeight = realHeightTotal - topHeightTotal - bottomHeightTotal;
            double viewBoxHeight = Round(realHeightTotal * 1000);
            var pathStr = SvgGeometry.TallDelim(svgLabel, Round(midHeight * 1000));
            var path = new PathNode(svgLabel, pathStr);
            var width = Units.MakeEm(viewBoxWidth / 1000.0);
            var height = Units.MakeEm(viewBoxHeight / 1000);
            var svg = new SvgNode(new List<VirtualNode> { path }, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["width"] = width,
                ["height"] = height,
                ["viewBox"] = $"0 0 {JsNumber.ToString(viewBoxWidth)} {JsNumber.ToString(viewBoxHeight)}",
            });
            var wrapper = BuildCommon.MakeSvgSpan(new List<string>(), new List<VirtualNode> { svg }, options);
            wrapper.Height = viewBoxHeight / 1000;
            wrapper.Style.Width = width;
            wrapper.Style.Height = height;
            stack.Add(new VListElem { Type = "elem", Elem = wrapper });
        }
        else
        {
            // Stack glyphs
            // Start by adding the bottom symbol
            stack.Add(MakeGlyphSpan(bottom, font, mode, options));
            stack.Add(lap); // overlap

            if (middle == null)
            {
                // The middle section will be an SVG. Make it an extra 0.016em tall.
                // We'll overlap by 0.008em at top and bottom.
                double innerHeight = realHeightTotal - topHeightTotal - bottomHeightTotal
                    + 2 * LapInEms;
                stack.Add(MakeInner(repeat, innerHeight, options));
            }
            else
            {
                // When there is a middle bit, we need the middle part and two repeated
                // sections
                double innerHeight = (realHeightTotal - topHeightTotal -
                    bottomHeightTotal - middleHeightTotal) / 2 + 2 * LapInEms;
                stack.Add(MakeInner(repeat, innerHeight, options));
                // Now insert the middle of the brace.
                stack.Add(lap);
                stack.Add(MakeGlyphSpan(middle, font, mode, options));
                stack.Add(lap);
                stack.Add(MakeInner(repeat, innerHeight, options));
            }

            // Add the top symbol
            stack.Add(lap);
            stack.Add(MakeGlyphSpan(top, font, mode, options));
        }

        // Finally, build the vlist
        var newOptions = options.HavingBaseStyle(Style.TEXT);
        var inner = BuildCommon.MakeVList(VListParam.Bottom(depth, stack), newOptions);

        return StyleWrap(
            BuildCommon.MakeSpan(new List<string> { "delimsizing", "mult" },
                new List<VirtualNode> { inner }, newOptions),
            Style.TEXT, options, classes);
    }

    // All surds have 0.08em padding above the vinculum inside the SVG.
    // That keeps browser span height rounding error from pinching the line.
    private const int VbPad = 80;   // padding above the surd, measured inside the viewBox.
    private const double EmPad = 0.08; // padding, in ems, measured in the document.

    private static Span SqrtSvg(
        string sqrtName,
        double height,
        double viewBoxHeight,
        double extraVinculum,
        Options options)
    {
        var path = SvgGeometry.SqrtPath(sqrtName, extraVinculum, viewBoxHeight);
        var pathNode = new PathNode(sqrtName, path);

        var svg = new SvgNode(new List<VirtualNode> { pathNode }, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Note: 1000:1 ratio of viewBox to document em width.
            ["width"] = "400em",
            ["height"] = Units.MakeEm(height),
            ["viewBox"] = "0 0 400000 " + JsNumber.ToString(viewBoxHeight),
            ["preserveAspectRatio"] = "xMinYMin slice",
        });

        return BuildCommon.MakeSvgSpan(new List<string> { "hide-tail" },
            new List<VirtualNode> { svg }, options);
    }

    /// <summary>
    /// Make a sqrt image of the given height,
    /// </summary>
    public static SqrtImage MakeSqrtImage(
        double height,
        Options options)
    {
        // Define a newOptions that removes the effect of size changes such as \Huge.
        // We don't pick different a height surd for \Huge. For it, we scale up.
        var newOptions = options.HavingBaseSizing();

        // Pick the desired surd glyph from a sequence of surds.
        var delim = TraverseSequence("\\surd", height * newOptions.SizeMultiplier,
            StackLargeDelimiterSequence, newOptions);

        double sizeMultiplier = newOptions.SizeMultiplier;  // default

        // The standard sqrt SVGs each have a 0.04em thick vinculum.
        // If Settings.minRuleThickness is larger than that, we add extraVinculum.
        double extraVinculum = Math.Max(0,
            options.MinRuleThickness - options.FontMetrics().SqrtRuleThickness);

        // Create a span containing an SVG image of a sqrt symbol.
        Span span;
        double spanHeight;
        double texHeight;
        double viewBoxHeight;
        double advanceWidth;

        // We create viewBoxes with 80 units of "padding" above each surd.
        // Then browser rounding error on the parent span height will not
        // encroach on the ink of the vinculum. But that padding is not
        // included in the TeX-like `height` used for calculation of
        // vertical alignment. So texHeight = span.height < span.style.height.

        if (delim.Type == "small")
        {
            // Get an SVG that is derived from glyph U+221A in font KaTeX-Main.
            // 1000 unit normal glyph height.
            viewBoxHeight = 1000 + 1000 * extraVinculum + VbPad;
            if (height < 1.0)
            {
                sizeMultiplier = 1.0;   // mimic a \textfont radical
            }
            else if (height < 1.4)
            {
                sizeMultiplier = 0.7;   // mimic a \scriptfont radical
            }
            spanHeight = (1.0 + extraVinculum + EmPad) / sizeMultiplier;
            texHeight = (1.00 + extraVinculum) / sizeMultiplier;
            span = SqrtSvg("sqrtMain", spanHeight, viewBoxHeight, extraVinculum,
                options);
            span.Style.MinWidth = "0.853em";
            advanceWidth = 0.833 / sizeMultiplier;  // from the font.
        }
        else if (delim.Type == "large")
        {
            // These SVGs come from fonts: KaTeX_Size1, _Size2, etc.
            viewBoxHeight = (1000 + VbPad) * SizeToMaxHeight[delim.Size];
            texHeight = (SizeToMaxHeight[delim.Size] + extraVinculum) / sizeMultiplier;
            spanHeight = (SizeToMaxHeight[delim.Size] + extraVinculum + EmPad)
                / sizeMultiplier;
            span = SqrtSvg("sqrtSize" + JsNumber.ToString(delim.Size), spanHeight, viewBoxHeight,
                extraVinculum, options);
            span.Style.MinWidth = "1.02em";
            advanceWidth = 1.0 / sizeMultiplier; // 1.0 from the font.
        }
        else
        {
            // Tall sqrt. In TeX, this would be stacked using multiple glyphs.
            // We'll use a single SVG to accomplish the same thing.
            spanHeight = height + extraVinculum + EmPad;
            texHeight = height + extraVinculum;
            viewBoxHeight = Math.Floor(1000 * height + extraVinculum) + VbPad;
            span = SqrtSvg("sqrtTall", spanHeight, viewBoxHeight, extraVinculum,
                options);
            span.Style.MinWidth = "0.742em";
            advanceWidth = 1.056;
        }

        span.Height = texHeight;
        span.Style.Height = Units.MakeEm(spanHeight);

        return new SqrtImage
        {
            Span = span,
            AdvanceWidth = advanceWidth,
            // Calculate the actual line width.
            // This actually should depend on the chosen font -- e.g. \boldmath
            // should use the thicker surd symbols from e.g. KaTeX_Main-Bold, and
            // have thicker rules.
            RuleWidth = (options.FontMetrics().SqrtRuleThickness + extraVinculum)
                * sizeMultiplier,
        };
    }

    // There are three kinds of delimiters, delimiters that stack when they become
    // too large
    private static readonly HashSet<string> StackLargeDelimiters = new(StringComparer.Ordinal)
    {
        "(", "\\lparen", ")", "\\rparen",
        "[", "\\lbrack", "]", "\\rbrack",
        "\\{", "\\lbrace", "\\}", "\\rbrace",
        "\\lfloor", "\\rfloor", "\u230a", "\u230b",
        "\\lceil", "\\rceil", "\u2308", "\u2309",
        "\\surd",
    };

    // delimiters that always stack
    private static readonly HashSet<string> StackAlwaysDelimiters = new(StringComparer.Ordinal)
    {
        "\\uparrow", "\\downarrow", "\\updownarrow",
        "\\Uparrow", "\\Downarrow", "\\Updownarrow",
        "|", "\\|", "\\vert", "\\Vert",
        "\\lvert", "\\rvert", "\\lVert", "\\rVert",
        "\\lgroup", "\\rgroup", "\u27ee", "\u27ef",
        "\\lmoustache", "\\rmoustache", "\u23b0", "\u23b1",
    };

    // and delimiters that never stack
    private static readonly HashSet<string> StackNeverDelimiters = new(StringComparer.Ordinal)
    {
        "<", ">", "\\langle", "\\rangle", "/", "\\backslash", "\\lt", "\\gt",
    };

    // Metrics of the different sizes. Found by looking at TeX's output of
    // $\bigl| // \Bigl| \biggl| \Biggl| \showlists$
    // Used to create stacked delimiters of appropriate sizes in MakeSizedDelim.
    public static readonly double[] SizeToMaxHeight = { 0, 1.2, 1.8, 2.4, 3.0 };

    /// <summary>
    /// Used to create a delimiter of a specific size, where `size` is 1, 2, 3, or 4.
    /// </summary>
    public static Span MakeSizedDelim(
        string delim,
        int size,
        Options options,
        string mode,
        List<string> classes)
    {
        // < and > turn into \langle and \rangle in delimiters
        if (delim == "<" || delim == "\\lt" || delim == "\u27e8")
        {
            delim = "\\langle";
        }
        else if (delim == ">" || delim == "\\gt" || delim == "\u27e9")
        {
            delim = "\\rangle";
        }

        // Sized delimiters are never centered.
        if (StackLargeDelimiters.Contains(delim) || StackNeverDelimiters.Contains(delim))
        {
            return MakeLargeDelim(delim, size, false, options, mode, classes);
        }
        else if (StackAlwaysDelimiters.Contains(delim))
        {
            return MakeStackedDelim(
                delim,
                SizeToMaxHeight[size],
                false,
                options,
                mode,
                classes);
        }
        else
        {
            throw new ParseError("Illegal delimiter: '" + delim + "'");
        }
    }

    public static Span MakeSizedDelim(
        string delim,
        double size,
        Options options,
        string mode,
        List<string> classes)
        => MakeSizedDelim(delim, (int)size, options, mode, classes);

    // Delimiters that never stack try small delimiters and large delimiters only
    private static readonly DelimiterSpec[] StackNeverDelimiterSequence =
    {
        new() { Type = "small", Style = Style.SCRIPTSCRIPT },
        new() { Type = "small", Style = Style.SCRIPT },
        new() { Type = "small", Style = Style.TEXT },
        new() { Type = "large", Size = 1 },
        new() { Type = "large", Size = 2 },
        new() { Type = "large", Size = 3 },
        new() { Type = "large", Size = 4 },
    };

    // Delimiters that always stack try the small delimiters first, then stack
    private static readonly DelimiterSpec[] StackAlwaysDelimiterSequence =
    {
        new() { Type = "small", Style = Style.SCRIPTSCRIPT },
        new() { Type = "small", Style = Style.SCRIPT },
        new() { Type = "small", Style = Style.TEXT },
        new() { Type = "stack" },
    };

    // Delimiters that stack when large try the small and then large delimiters, and
    // stack afterwards
    private static readonly DelimiterSpec[] StackLargeDelimiterSequence =
    {
        new() { Type = "small", Style = Style.SCRIPTSCRIPT },
        new() { Type = "small", Style = Style.SCRIPT },
        new() { Type = "small", Style = Style.TEXT },
        new() { Type = "large", Size = 1 },
        new() { Type = "large", Size = 2 },
        new() { Type = "large", Size = 3 },
        new() { Type = "large", Size = 4 },
        new() { Type = "stack" },
    };

    /// <summary>
    /// Get the font used in a delimiter based on what kind of delimiter it is.
    /// TODO(#963) Use more specific font family return type once that is introduced.
    /// </summary>
    private static string DelimTypeToFont(DelimiterSpec type)
    {
        if (type.Type == "small")
        {
            return "Main-Regular";
        }
        else if (type.Type == "large")
        {
            return "Size" + JsNumber.ToString(type.Size) + "-Regular";
        }
        else if (type.Type == "stack")
        {
            return "Size4-Regular";
        }
        else
        {
            var delimKind = type.Type;
            throw new Exception($"Add support for delim type '{delimKind}' here.");
        }
    }

    /// <summary>
    /// Traverse a sequence of types of delimiters to decide what kind of delimiter
    /// should be used to create a delimiter of the given height+depth.
    /// </summary>
    private static DelimiterSpec TraverseSequence(
        string delim,
        double height,
        DelimiterSpec[] sequence,
        Options options)
    {
        // Here, we choose the index we should start at in the sequences. In smaller
        // sizes (which correspond to larger numbers in style.size) we start earlier
        // in the sequence. Thus, scriptscript starts at index 3-3=0, script starts
        // at index 3-2=1, text starts at 3-1=2, and display starts at min(2,3-0)=2
        int start = Math.Min(2, 3 - options.Style.Size);
        for (int i = start; i < sequence.Length; i++)
        {
            var delimType = sequence[i];
            if (delimType.Type == "stack")
            {
                // This is always the last delimiter, so we just break the loop now.
                break;
            }

            var metrics = GetMetrics(delim, DelimTypeToFont(delimType), "math");
            double heightDepth = metrics.Height + metrics.Depth;

            // Small delimiters are scaled down versions of the same font, so we
            // account for the style change size.
            if (delimType.Type == "small")
            {
                var newOptions = options.HavingBaseStyle(delimType.Style);
                heightDepth *= newOptions.SizeMultiplier;
            }

            // Check if the delimiter at this size works for the given height.
            if (heightDepth > height)
            {
                return delimType;
            }
        }

        // If we reached the end of the sequence, return the last sequence element.
        return sequence[sequence.Length - 1];
    }

    /// <summary>
    /// Make a delimiter of a given height+depth, with optional centering. Here, we
    /// traverse the sequences, and create a delimiter that the sequence tells us to.
    /// </summary>
    public static Span MakeCustomSizedDelim(
        string delim,
        double height,
        bool center,
        Options options,
        string mode,
        List<string> classes)
    {
        if (delim == "<" || delim == "\\lt" || delim == "\u27e8")
        {
            delim = "\\langle";
        }
        else if (delim == ">" || delim == "\\gt" || delim == "\u27e9")
        {
            delim = "\\rangle";
        }

        // Decide what sequence to use
        DelimiterSpec[] sequence;
        if (StackNeverDelimiters.Contains(delim))
        {
            sequence = StackNeverDelimiterSequence;
        }
        else if (StackLargeDelimiters.Contains(delim))
        {
            sequence = StackLargeDelimiterSequence;
        }
        else
        {
            sequence = StackAlwaysDelimiterSequence;
        }

        // Look through the sequence
        var delimType = TraverseSequence(delim, height, sequence, options);

        // Get the delimiter from font glyphs.
        // Depending on the sequence element we decided on, call the
        // appropriate function.
        if (delimType.Type == "small")
        {
            return MakeSmallDelim(delim, delimType.Style, center, options,
                                  mode, classes);
        }
        else if (delimType.Type == "large")
        {
            return MakeLargeDelim(delim, delimType.Size, center, options, mode,
                                  classes);
        }
        else /* if (delimType.type === "stack") */
        {
            return MakeStackedDelim(delim, height, center, options, mode,
                                    classes);
        }
    }

    /// <summary>
    /// Make a delimiter for use with `\left` and `\right`, given a height and depth
    /// of an expression that the delimiters surround.
    /// </summary>
    public static Span MakeLeftRightDelim(
        string delim,
        double height,
        double depth,
        Options options,
        string mode,
        List<string> classes)
    {
        // We always center \left/\right delimiters, so the axis is always shifted
        double axisHeight =
            options.FontMetrics().AxisHeight * options.SizeMultiplier;

        // Taken from TeX source, tex.web, function make_left_right
        const int delimiterFactor = 901;
        double delimiterExtend = 5.0 / options.FontMetrics().PtPerEm;

        double maxDistFromAxis = Math.Max(
            height - axisHeight, depth + axisHeight);

        double totalHeight = Math.Max(
            // In real TeX, calculations are done using integral values which are
            // 65536 per pt, or 655360 per em. So, the division here truncates in
            // TeX but doesn't here, producing different results. If we wanted to
            // exactly match TeX's calculation, we could do
            //   Math.floor(655360 * maxDistFromAxis / 500) *
            //    delimiterFactor / 655360
            // (To see the difference, compare
            //    x^{x^{\left(\rule{0.1em}{0.68em}\right)}}
            // in TeX and KaTeX)
            maxDistFromAxis / 500 * delimiterFactor,
            2 * maxDistFromAxis - delimiterExtend);

        // Finally, we defer to `MakeCustomSizedDelim` with our calculated total
        // height
        return MakeCustomSizedDelim(delim, totalHeight, true, options, mode, classes);
    }
}

using System.Text.RegularExpressions;

namespace KStyleMath;

/// <summary>
/// This file provides support to buildMathML.js and buildHTML.js
/// for stretchy wide elements rendered from SVG files
/// and other CSS trickery.
/// </summary>
public static class Stretchy
{
    private static readonly Dictionary<string, string> StretchyCodePoint = new(StringComparer.Ordinal)
    {
        ["widehat"] = "^",
        ["widecheck"] = "\u02c7",
        ["widetilde"] = "~",
        ["utilde"] = "~",
        ["overleftarrow"] = "\u2190",
        ["underleftarrow"] = "\u2190",
        ["xleftarrow"] = "\u2190",
        ["overrightarrow"] = "\u2192",
        ["underrightarrow"] = "\u2192",
        ["xrightarrow"] = "\u2192",
        ["underbrace"] = "\u23df",
        ["overbrace"] = "\u23de",
        ["underbracket"] = "\u23b5",
        ["overbracket"] = "\u23b4",
        ["overgroup"] = "\u23e0",
        ["undergroup"] = "\u23e1",
        ["overleftrightarrow"] = "\u2194",
        ["underleftrightarrow"] = "\u2194",
        ["xleftrightarrow"] = "\u2194",
        ["Overrightarrow"] = "\u21d2",
        ["xRightarrow"] = "\u21d2",
        ["overleftharpoon"] = "\u21bc",
        ["xleftharpoonup"] = "\u21bc",
        ["overrightharpoon"] = "\u21c0",
        ["xrightharpoonup"] = "\u21c0",
        ["xLeftarrow"] = "\u21d0",
        ["xLeftrightarrow"] = "\u21d4",
        ["xhookleftarrow"] = "\u21a9",
        ["xhookrightarrow"] = "\u21aa",
        ["xmapsto"] = "\u21a6",
        ["xrightharpoondown"] = "\u21c1",
        ["xleftharpoondown"] = "\u21bd",
        ["xrightleftharpoons"] = "\u21cc",
        ["xleftrightharpoons"] = "\u21cb",
        ["xtwoheadleftarrow"] = "\u219e",
        ["xtwoheadrightarrow"] = "\u21a0",
        ["xlongequal"] = "=",
        ["xtofrom"] = "\u21c4",
        ["xrightleftarrows"] = "\u21c4",
        ["xrightequilibrium"] = "\u21cc",  // Not a perfect match.
        ["xleftequilibrium"] = "\u21cb",   // None better available.
        ["\\cdrightarrow"] = "\u2192",
        ["\\cdleftarrow"] = "\u2190",
        ["\\cdlongequal"] = "=",
    };

    public static MathNode StretchyMathML(string label)
    {
        var key = Regex.Replace(label, "^\\\\", "");
        StretchyCodePoint.TryGetValue(key, out var codePoint);
        var node = new MathNode(
            "mo",
            new List<VirtualNode>
            {
                new MathTextNode(codePoint),
            });
        node.SetAttribute("stretchy", "true");
        return node;
    }

    // Many of the KaTeX SVG images have been adapted from glyphs in KaTeX fonts.
    // Copyright (c) 2009-2010, Design Science, Inc. (<www.mathjax.org>)
    // Copyright (c) 2014-2017 Khan Academy (<www.khanacademy.org>)
    // Licensed under the SIL Open Font License, Version 1.1.
    // See \nhttp://scripts.sil.org/OFL

    // Very Long SVGs
    //    Many of the KaTeX stretchy wide elements use a long SVG image and an
    //    overflow: hidden tactic to achieve a stretchy image while avoiding
    //    distortion of arrowheads or brace corners.

    //    The SVG typically contains a very long (400 em) arrow.

    //    The SVG is in a container span that has overflow: hidden, so the span
    //    acts like a window that exposes only part of the  SVG.

    //    The SVG always has a longer, thinner aspect ratio than the container span.
    //    After the SVG fills 100% of the height of the container span,
    //    there is a long arrow shaft left over. That left-over shaft is not shown.
    //    Instead, it is sliced off because the span's CSS has overflow: hidden.

    //    Thus, the reader sees an arrow that matches the subject matter width
    //    without distortion.

    //    Some functions, such as \cancel, need to vary their aspect ratio. These
    //    functions do not get the overflow SVG treatment.

    // In the katexImagesData object just below, the dimensions all
    // correspond to path geometry inside the relevant SVG.
    // For example, \overrightarrow uses the same arrowhead as glyph U+2192
    // from the KaTeX Main font. The scaling factor is 1000.
    // That is, inside the font, that arrowhead is 522 units tall, which
    // corresponds to 0.522 em inside the document.

    private sealed class SvgData
    {
        public string[] Paths;
        public double MinWidth;
        public double ViewBoxHeight;
        public string Align;
        public bool HasAlign;
    }

    private static SvgData Svg(string[] paths, double minWidth, double height, string align = null)
        => new SvgData
        {
            Paths = paths,
            MinWidth = minWidth,
            ViewBoxHeight = height,
            Align = align,
            HasAlign = align != null,
        };

    private static readonly Dictionary<string, SvgData> KatexImagesData = new(StringComparer.Ordinal)
    {
        //                   path(s), minWidth, height, align
        ["overrightarrow"] = Svg(new[] { "rightarrow" }, 0.888, 522, "xMaxYMin"),
        ["overleftarrow"] = Svg(new[] { "leftarrow" }, 0.888, 522, "xMinYMin"),
        ["underrightarrow"] = Svg(new[] { "rightarrow" }, 0.888, 522, "xMaxYMin"),
        ["underleftarrow"] = Svg(new[] { "leftarrow" }, 0.888, 522, "xMinYMin"),
        ["xrightarrow"] = Svg(new[] { "rightarrow" }, 1.469, 522, "xMaxYMin"),
        ["\\cdrightarrow"] = Svg(new[] { "rightarrow" }, 3.0, 522, "xMaxYMin"), // CD minwwidth2.5pc
        ["xleftarrow"] = Svg(new[] { "leftarrow" }, 1.469, 522, "xMinYMin"),
        ["\\cdleftarrow"] = Svg(new[] { "leftarrow" }, 3.0, 522, "xMinYMin"),
        ["Overrightarrow"] = Svg(new[] { "doublerightarrow" }, 0.888, 560, "xMaxYMin"),
        ["xRightarrow"] = Svg(new[] { "doublerightarrow" }, 1.526, 560, "xMaxYMin"),
        ["xLeftarrow"] = Svg(new[] { "doubleleftarrow" }, 1.526, 560, "xMinYMin"),
        ["overleftharpoon"] = Svg(new[] { "leftharpoon" }, 0.888, 522, "xMinYMin"),
        ["xleftharpoonup"] = Svg(new[] { "leftharpoon" }, 0.888, 522, "xMinYMin"),
        ["xleftharpoondown"] = Svg(new[] { "leftharpoondown" }, 0.888, 522, "xMinYMin"),
        ["overrightharpoon"] = Svg(new[] { "rightharpoon" }, 0.888, 522, "xMaxYMin"),
        ["xrightharpoonup"] = Svg(new[] { "rightharpoon" }, 0.888, 522, "xMaxYMin"),
        ["xrightharpoondown"] = Svg(new[] { "rightharpoondown" }, 0.888, 522, "xMaxYMin"),
        ["xlongequal"] = Svg(new[] { "longequal" }, 0.888, 334, "xMinYMin"),
        ["\\cdlongequal"] = Svg(new[] { "longequal" }, 3.0, 334, "xMinYMin"),
        ["xtwoheadleftarrow"] = Svg(new[] { "twoheadleftarrow" }, 0.888, 334, "xMinYMin"),
        ["xtwoheadrightarrow"] = Svg(new[] { "twoheadrightarrow" }, 0.888, 334, "xMaxYMin"),

        ["overleftrightarrow"] = Svg(new[] { "leftarrow", "rightarrow" }, 0.888, 522),
        ["overbrace"] = Svg(new[] { "leftbrace", "midbrace", "rightbrace" }, 1.6, 548),
        ["underbrace"] = Svg(new[] { "leftbraceunder", "midbraceunder", "rightbraceunder" },
            1.6, 548),
        ["underleftrightarrow"] = Svg(new[] { "leftarrow", "rightarrow" }, 0.888, 522),
        ["xleftrightarrow"] = Svg(new[] { "leftarrow", "rightarrow" }, 1.75, 522),
        ["xLeftrightarrow"] = Svg(new[] { "doubleleftarrow", "doublerightarrow" }, 1.75, 560),
        ["xrightleftharpoons"] = Svg(new[] { "leftharpoondownplus", "rightharpoonplus" }, 1.75, 716),
        ["xleftrightharpoons"] = Svg(new[] { "leftharpoonplus", "rightharpoondownplus" },
            1.75, 716),
        ["xhookleftarrow"] = Svg(new[] { "leftarrow", "righthook" }, 1.08, 522),
        ["xhookrightarrow"] = Svg(new[] { "lefthook", "rightarrow" }, 1.08, 522),
        ["overlinesegment"] = Svg(new[] { "leftlinesegment", "rightlinesegment" }, 0.888, 522),
        ["underlinesegment"] = Svg(new[] { "leftlinesegment", "rightlinesegment" }, 0.888, 522),
        ["overbracket"] = Svg(new[] { "leftbracketover", "rightbracketover" }, 1.6, 440),
        ["underbracket"] = Svg(new[] { "leftbracketunder", "rightbracketunder" }, 1.6, 410),
        ["overgroup"] = Svg(new[] { "leftgroup", "rightgroup" }, 0.888, 342),
        ["undergroup"] = Svg(new[] { "leftgroupunder", "rightgroupunder" }, 0.888, 342),
        ["xmapsto"] = Svg(new[] { "leftmapsto", "rightarrow" }, 1.5, 522),
        ["xtofrom"] = Svg(new[] { "leftToFrom", "rightToFrom" }, 1.75, 528),

        // The next three arrows are from the mhchem package.
        // In mhchem.sty, min-length is 2.0em. But these arrows might appear in the
        // document as \xrightarrow or \xrightleftharpoons. Those have
        // min-length = 1.75em, so we set min-length on these next three to match.
        ["xrightleftarrows"] = Svg(new[] { "baraboveleftarrow", "rightarrowabovebar" }, 1.75, 901),
        ["xrightequilibrium"] = Svg(new[]
        {
            "baraboveshortleftharpoon",
            "rightharpoonaboveshortbar",
        }, 1.75, 716),
        ["xleftequilibrium"] = Svg(new[]
        {
            "shortbaraboveleftharpoon",
            "shortrightharpoonabovebar",
        }, 1.75, 716),
    };

    private static readonly HashSet<string> WideAccentLabels = new(StringComparer.Ordinal)
        { "widehat", "widecheck", "widetilde", "utilde" };

    public static Span StretchySvg(
        ParseNode group,
        Options options)
    {
        // Create a span with inline SVG for the element.
        (Span span, double minWidth, double height) BuildSvgSpan()
        {
            int viewBoxWidth = 400000;  // default
            var label = group.Str("label").Substring(1);
            if (WideAccentLabels.Contains(label) && group.Has("base"))
            {
                // There are four SVG images available for each function.
                // Choose a taller image when there are more characters.
                var baseNode = group.Node("base");
                var numChars = baseNode.Type == "ordgroup" ?
                    baseNode.List("body").Count : 1;
                double viewBoxHeight;
                string pathName;
                double height;

                if (numChars > 5)
                {
                    if (label == "widehat" || label == "widecheck")
                    {
                        viewBoxHeight = 420;
                        viewBoxWidth = 2364;
                        height = 0.42;
                        pathName = label + "4";
                    }
                    else
                    {
                        viewBoxHeight = 312;
                        viewBoxWidth = 2340;
                        height = 0.34;
                        pathName = "tilde4";
                    }
                }
                else
                {
                    var imgIndices = new[] { 1, 1, 2, 2, 3, 3 };
                    var imgIndex = imgIndices[numChars];
                    if (label == "widehat" || label == "widecheck")
                    {
                        var widths = new[] { 0, 1062, 2364, 2364, 2364 };
                        var heights = new[] { 0, 239, 300, 360, 420 };
                        var heightsEm = new[] { 0.0, 0.24, 0.3, 0.3, 0.36, 0.42 };
                        viewBoxWidth = widths[imgIndex];
                        viewBoxHeight = heights[imgIndex];
                        height = heightsEm[imgIndex];
                        pathName = label + JsNumber.ToString(imgIndex);
                    }
                    else
                    {
                        var widths = new[] { 0, 600, 1033, 2339, 2340 };
                        var heights = new[] { 0, 260, 286, 306, 312 };
                        var heightsEm = new[] { 0.0, 0.26, 0.286, 0.3, 0.306, 0.34 };
                        viewBoxWidth = widths[imgIndex];
                        viewBoxHeight = heights[imgIndex];
                        height = heightsEm[imgIndex];
                        pathName = "tilde" + JsNumber.ToString(imgIndex);
                    }
                }
                var path = new PathNode(pathName);
                var svgNode = new SvgNode(new List<VirtualNode> { path },
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["width"] = "100%",
                        ["height"] = Units.MakeEm(height),
                        ["viewBox"] = $"0 0 {JsNumber.ToString(viewBoxWidth)} {JsNumber.ToString(viewBoxHeight)}",
                        ["preserveAspectRatio"] = "none",
                    });
                return (
                    BuildCommon.MakeSvgSpan(new List<string>(),
                        new List<VirtualNode> { svgNode }, options),
                    0,
                    height);
            }
            else
            {
                var spans = new List<VirtualNode>();

                if (!KatexImagesData.TryGetValue(label, out var data))
                {
                    throw new Exception($"No SVG data for \"{label}\".");
                }
                var paths = data.Paths;
                var minWidth = data.MinWidth;
                var viewBoxHeight = data.ViewBoxHeight;
                var height = viewBoxHeight / 1000;

                var numSvgChildren = paths.Length;
                List<string> widthClasses;
                List<string> aligns;
                if (numSvgChildren == 1)
                {
                    if (!data.HasAlign)
                    {
                        throw new Exception(
                            $"Expected 4-tuple for single-path SVG data \"{label}\".");
                    }
                    widthClasses = new List<string> { "hide-tail" };
                    aligns = new List<string> { data.Align };
                }
                else if (numSvgChildren == 2)
                {
                    widthClasses = new List<string> { "halfarrow-left", "halfarrow-right" };
                    aligns = new List<string> { "xMinYMin", "xMaxYMin" };
                }
                else if (numSvgChildren == 3)
                {
                    widthClasses = new List<string> { "brace-left", "brace-center", "brace-right" };
                    aligns = new List<string> { "xMinYMin", "xMidYMin", "xMaxYMin" };
                }
                else
                {
                    throw new Exception(
                        $"Correct katexImagesData or update code here to support\n                    {JsNumber.ToString(numSvgChildren)} children.");
                }

                for (int i = 0; i < numSvgChildren; i++)
                {
                    var path = new PathNode(paths[i]);

                    var svgNode = new SvgNode(new List<VirtualNode> { path },
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["width"] = "400em",
                            ["height"] = Units.MakeEm(height),
                            ["viewBox"] = $"0 0 {JsNumber.ToString(viewBoxWidth)} {JsNumber.ToString(viewBoxHeight)}",
                            ["preserveAspectRatio"] = aligns[i] + " slice",
                        });

                    var span = BuildCommon.MakeSvgSpan(
                        new List<string> { widthClasses[i] },
                        new List<VirtualNode> { svgNode }, options);
                    if (numSvgChildren == 1)
                    {
                        return (span, minWidth, height);
                    }
                    else
                    {
                        span.Style.Height = Units.MakeEm(height);
                        spans.Add(span);
                    }
                }

                return (
                    BuildCommon.MakeSpan(new List<string> { "katex-stretchy" }, spans, options),
                    minWidth,
                    height);
            }
        } // buildSvgSpan_()

        var (span, minWidth, height) = BuildSvgSpan();

        // Note that we are returning span.depth = 0.
        // Any adjustments relative to the baseline must be done in buildHTML.
        span.Height = height;
        span.Style.Height = Units.MakeEm(height);
        if (minWidth > 0)
        {
            span.Style.MinWidth = Units.MakeEm(minWidth);
        }

        return span;
    }

    public static Span StretchyEnclose(
        HtmlDomNode inner,
        string label,
        double topPad,
        double bottomPad,
        Options options)
    {
        // Return an image span for \cancel, \bcancel, \xcancel, \fbox, or \angl
        Span img;
        double totalHeight = inner.Height + inner.Depth + topPad + bottomPad;

        if (Regex.IsMatch(label, "fbox|color|angl"))
        {
            img = BuildCommon.MakeSpan(new List<string> { "katex-stretchy", label },
                new List<VirtualNode>(), options);

            if (label == "fbox")
            {
                var color = !string.IsNullOrEmpty(options.Color) && options.GetColor() != null;
                if (color)
                {
                    img.Style.BorderColor = options.GetColor();
                }
            }
        }
        else
        {
            // \cancel, \bcancel, or \xcancel
            // Since \cancel's SVG is inline and it omits the viewBox attribute,
            // its stroke-width will not vary with span area.

            var lines = new List<VirtualNode>();
            if (Regex.IsMatch(label, "^[bx]cancel$"))
            {
                lines.Add(new LineNode(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["x1"] = "0",
                    ["y1"] = "0",
                    ["x2"] = "100%",
                    ["y2"] = "100%",
                    ["stroke-width"] = "0.046em",
                }));
            }

            if (Regex.IsMatch(label, "^x?cancel$"))
            {
                lines.Add(new LineNode(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["x1"] = "0",
                    ["y1"] = "100%",
                    ["x2"] = "100%",
                    ["y2"] = "0",
                    ["stroke-width"] = "0.046em",
                }));
            }

            var svgNode = new SvgNode(lines, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["width"] = "100%",
                ["height"] = Units.MakeEm(totalHeight),
            });

            img = BuildCommon.MakeSvgSpan(new List<string>(),
                new List<VirtualNode> { svgNode }, options);
        }

        img.Height = totalHeight;
        img.Style.Height = Units.MakeEm(totalHeight);

        return img;
    }
}

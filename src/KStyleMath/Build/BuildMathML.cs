using System.Linq;

namespace KStyleMath;

/// <summary>
/// This file converts a parse tree into a corresponding MathML tree. The main
/// entry point is the <see cref="Build"/> function, which takes a parse tree
/// from the parser.
/// </summary>
public static class BuildMathML
{
    private static readonly HashSet<string> NoVariantSymbols =
        new(StringComparer.Ordinal) { "\\imath", "\\jmath" };

    private static readonly HashSet<string> RowLikeTypes =
        new(StringComparer.Ordinal) { "mrow", "mtable" };

    /// <summary>
    /// Takes a symbol and converts it into a MathML text node after performing
    /// optional replacement from symbols.js.
    /// </summary>
    public static VirtualNode MakeText(string text, string mode, Options options = null)
    {
        var symbol = text == null ? null : Symbols.Get(mode, text);
        if (symbol != null && symbol.Replace != null &&
            (text.Length == 0 || text[0] != 0xD835) &&
            !(Symbols.Ligatures.ContainsKey(text) &&
              (Slice4To6(options?.FontFamily) == "tt" ||
               Slice4To6(options?.Font) == "tt")))
        {
            text = symbol.Replace;
        }

        return new MathTextNode(text);
    }

    /// <summary>JavaScript String.prototype.slice(4, 6).</summary>
    private static string Slice4To6(string s)
    {
        if (s == null)
        {
            return null;
        }
        if (s.Length <= 4)
        {
            return "";
        }
        return s.Substring(4, Math.Min(2, s.Length - 4));
    }

    /// <summary>
    /// Wrap the given array of nodes in an &lt;mrow&gt; node if needed, i.e.,
    /// unless the array has length 1.  Always returns a single node.
    /// </summary>
    public static VirtualNode MakeRow(IEnumerable<VirtualNode> bodyEnumerable)
        => MakeRow(bodyEnumerable.ToList());

    public static VirtualNode MakeRow(List<VirtualNode> body)
    {
        if (body.Count == 1)
        {
            return body[0];
        }
        else
        {
            return new MathNode("mrow", body);
        }
    }

    private static readonly Dictionary<string, Func<ParseNode, string>> MathFontVariants =
        new(StringComparer.Ordinal)
        {
            ["mathit"] = group => "italic",
            ["boldsymbol"] = group => group.Type == "textord" ? "bold" : "bold-italic",
            ["mathbf"] = group => "bold",
            ["mathbb"] = group => "double-struck",
            ["mathsfit"] = group => "sans-serif-italic",
            ["mathfrak"] = group => "fraktur",
            ["mathscr"] = group => "script",
            ["mathcal"] = group => "script",
            ["mathsf"] = group => "sans-serif",
            ["mathtt"] = group => "monospace",
        };

    /// <summary>
    /// Returns the math variant as a string or null if none is required.
    /// </summary>
    public static string GetVariant(ParseNode group, Options options)
    {
        // Handle \text... font specifiers as best we can.
        // MathML has a limited list of allowable mathvariant specifiers; see
        // https://www.w3.org/TR/MathML3/chapter3.html#presm.commatt
        if (group.Mode == "text")
        {
            if (options.FontFamily == "texttt")
            {
                return "monospace";
            }
            else if (options.FontFamily == "textsf")
            {
                if (options.FontShape == "textit" &&
                    options.FontWeight == "textbf")
                {
                    return "sans-serif-bold-italic";
                }
                else if (options.FontShape == "textit")
                {
                    return "sans-serif-italic";
                }
                else if (options.FontWeight == "textbf")
                {
                    return "bold-sans-serif";
                }
                else
                {
                    return "sans-serif";
                }
            }
            else if (options.FontShape == "textit" &&
                     options.FontWeight == "textbf")
            {
                return "bold-italic";
            }
            else if (options.FontShape == "textit")
            {
                return "italic";
            }
            else if (options.FontWeight == "textbf")
            {
                return "bold";
            }
        }

        var font = options.Font;
        if (string.IsNullOrEmpty(font) || font == "mathnormal")
        {
            return null;
        }

        var mode = group.Mode;
        if (MathFontVariants.TryGetValue(font, out var mathVariant))
        {
            return mathVariant(group);
        }

        var text = group.Str("text");
        if (text != null && NoVariantSymbols.Contains(text))
        {
            return null;
        }

        var symbol = text == null ? null : Symbols.Get(mode, text);
        if (symbol != null)
        {
            var replacement = symbol.Replace;
            if (replacement != null)
            {
                text = replacement;
            }
        }

        var fontName = BuildCommon.FontMap[font].FontName;
        if (FontMetricsData.GetCharacterMetrics(text ?? "", fontName, mode) != null)
        {
            return BuildCommon.FontMap[font].Variant;
        }

        return null;
    }

    /// <summary>
    /// Check for &lt;mi&gt;.&lt;/mi&gt; which is how a dot renders in MathML,
    /// or &lt;mo separator="true" lspace="0em" rspace="0em"&gt;,&lt;/mo&gt;
    /// which is how a braced comma {,} renders in MathML
    /// </summary>
    private static bool IsNumberPunctuation(MathNode group)
    {
        if (group == null)
        {
            return false;
        }
        if (group.Type == "mi" && group.Children.Count == 1)
        {
            var child = group.Children[0];
            return child is MathTextNode textNode && textNode.Text == ".";
        }
        else if (group.Type == "mo" && group.Children.Count == 1 &&
                 group.GetAttribute("separator") == "true" &&
                 group.GetAttribute("lspace") == "0em" &&
                 group.GetAttribute("rspace") == "0em")
        {
            var child = group.Children[0];
            return child is MathTextNode textNode && textNode.Text == ",";
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Takes a list of nodes, builds them, and returns a list of the generated
    /// MathML nodes.  Also combine consecutive &lt;mtext&gt; outputs into a
    /// single &lt;mtext&gt; tag.
    /// </summary>
    public static List<VirtualNode> BuildExpression(List<ParseNode> expression, Options options,
        bool isOrdgroup = false)
    {
        if (expression.Count == 1)
        {
            var group = BuildGroup(expression[0], options);
            if (isOrdgroup && group is MathNode g && g.Type == "mo")
            {
                g.SetAttribute("lspace", "0em");
                g.SetAttribute("rspace", "0em");
            }
            return new List<VirtualNode> { group };
        }

        var groups = new List<VirtualNode>();
        VirtualNode lastGroup = null;
        for (int i = 0; i < expression.Count; i++)
        {
            var group = BuildGroup(expression[i], options);
            var gm = group as MathNode;
            var lastM = lastGroup as MathNode;
            if (lastM != null && gm != null)
            {
                if (gm.Type == "mtext" && lastM.Type == "mtext" &&
                    gm.GetAttribute("mathvariant") == lastM.GetAttribute("mathvariant"))
                {
                    lastM.Children.AddRange(gm.Children);
                    continue;
                }
                else if (gm.Type == "mn" && lastM.Type == "mn")
                {
                    lastM.Children.AddRange(gm.Children);
                    continue;
                }
                else if (IsNumberPunctuation(gm) && lastM.Type == "mn")
                {
                    lastM.Children.AddRange(gm.Children);
                    continue;
                }
                else if (gm.Type == "mn" && IsNumberPunctuation(lastM))
                {
                    gm.Children = lastM.Children.Concat(gm.Children).ToList();
                    groups.RemoveAt(groups.Count - 1);
                }
                else if ((gm.Type == "msup" || gm.Type == "msub") &&
                         gm.Children.Count >= 1 &&
                         (lastM.Type == "mn" || IsNumberPunctuation(lastM)))
                {
                    var baseNode = gm.Children[0];
                    if (baseNode is MathNode baseMathNode && baseMathNode.Type == "mn")
                    {
                        baseMathNode.Children = lastM.Children.Concat(baseMathNode.Children).ToList();
                        groups.RemoveAt(groups.Count - 1);
                    }
                }
                else if (lastM.Type == "mi" && lastM.Children.Count == 1)
                {
                    var lastChild = lastM.Children[0];
                    if (lastChild is MathTextNode lastTextNode && lastTextNode.Text == "\u0338" &&
                        (gm.Type == "mo" || gm.Type == "mi" || gm.Type == "mn"))
                    {
                        if (gm.Children.Count > 0)
                        {
                            var child = gm.Children[0];
                            if (child is MathTextNode childTextNode && childTextNode.Text.Length > 0)
                            {
                                childTextNode.Text = childTextNode.Text.Substring(0, 1) + "\u0338" +
                                    childTextNode.Text.Substring(1);
                                groups.RemoveAt(groups.Count - 1);
                            }
                        }
                    }
                }
            }
            groups.Add(group);
            lastGroup = group;
        }
        return groups;
    }
    /// <summary>
    /// Equivalent to buildExpression, but wraps the elements in an &lt;mrow&gt;
    /// if there's more than one.  Returns a single node instead of an array.
    /// </summary>
    public static VirtualNode BuildExpressionRow(List<ParseNode> expression, Options options,
        bool isOrdgroup = false)
    {
        return MakeRow(BuildExpression(expression, options, isOrdgroup));
    }

    /// <summary>
    /// Takes a group from the parser and calls the appropriate groupBuilders
    /// function on it to produce a MathML node.
    /// </summary>
    public static VirtualNode BuildGroup(ParseNode group, Options options)
    {
        if (group == null)
        {
            return new MathNode("mrow");
        }

        if (Registries.MathmlGroupBuilders.TryGetValue(group.Type, out var builder))
        {
            // TODO(ts): MathMLBuilder returns MathDomNode but all concrete
            // builders return MathNode. Widening the return type here would
            // require updating all callers that assume MathNode.
            return builder(group, options);
        }
        else
        {
            throw new ParseError(
                "Got group of unknown type: '" + group.Type + "'");
        }
    }

    /// <summary>
    /// Takes a full parse tree and settings and builds a MathML representation
    /// of it. In particular, we put the elements from building the parse tree
    /// into a &lt;semantics&gt; tag so we can also include that TeX source as
    /// an annotation.
    ///
    /// Note that we actually return a domTree element with a &lt;math&gt; inside
    /// it so we can do appropriate styling.
    /// </summary>
    public static Span Build(List<ParseNode> tree, string expression, Options options,
        bool isDisplayMode, bool forMathmlOnly)
    {
        var built = BuildExpression(tree, options);

        // TODO: Make a pass thru the MathML similar to buildHTML.traverseNonSpaceNodes
        // and add spacing nodes. This is necessary only adjacent to math operators
        // like \sin or \lim or to subsup elements that contain math operators.
        // MathML takes care of the other spacing issues.

        // Wrap up the expression in an mrow so it is presented in the semantics
        // tag correctly, unless it's a single <mrow> or <mtable>.
        VirtualNode wrapper;
        if (built.Count == 1 && built[0] is MathNode single &&
            RowLikeTypes.Contains(single.Type))
        {
            wrapper = built[0];
        }
        else
        {
            wrapper = new MathNode("mrow", built.Cast<VirtualNode>().ToList());
        }

        // Build a TeX annotation of the source
        var annotation = new MathNode(
            "annotation", new List<VirtualNode> { new MathTextNode(expression) });

        annotation.SetAttribute("encoding", "application/x-tex");

        var semantics = new MathNode(
            "semantics", new List<VirtualNode> { wrapper, annotation });

        var math = new MathNode("math", new List<VirtualNode> { semantics });
        math.SetAttribute("xmlns", "http://www.w3.org/1998/Math/MathML");
        if (isDisplayMode)
        {
            math.SetAttribute("display", "block");
        }

        // You can't style <math> nodes, so we wrap the node in a span.
        // NOTE: The span class is not typed to have <math> nodes as children, and
        // we don't want to make the children type more generic since the children
        // of span are expected to have more fields in `buildHtml` contexts.
        // The MathNode implements VirtualNode (toNode/toMarkup) which is all that
        // Span needs from its children for rendering.
        // TODO(ts): Span's child type is HtmlDomNode, but MathNode only implements
        // VirtualNode. The double-cast acknowledges this architectural limitation.
        var wrapperClass = forMathmlOnly ? "katex" : "katex-mathml";
        return BuildCommon.MakeSpan(
            new List<string> { wrapperClass },
            new List<VirtualNode> { math });
    }
}



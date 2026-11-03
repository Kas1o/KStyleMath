namespace KStyleMath;

public static class SupsubFunctions
{
    /// <summary>
    /// Sometimes, groups perform special rules when they have superscripts or
    /// subscripts attached to them. This function lets the `supsub` group know
    /// its inner element should handle the superscripts and subscripts instead of
    /// handling them itself.
    /// </summary>
    private static Func<ParseNode, Options, VirtualNode> HtmlBuilderDelegate(
        ParseNode group,
        Options options)
    {
        var baseNode = group.Node("base");
        if (baseNode == null)
        {
            return null;
        }
        else if (baseNode.Type == "op")
        {
            // Operators handle supsubs differently when they have limits
            // (e.g. `\displaystyle\sum_2^3`)
            var delegateBuilder = baseNode.Bool("limits") &&
                (options.Style.Size == Style.DISPLAY.Size ||
                 baseNode.Bool("alwaysHandleSupSub"));
            return delegateBuilder
                ? (Func<ParseNode, Options, VirtualNode>)OpFunctions.HtmlBuilder
                : null;
        }
        else if (baseNode.Type == "operatorname")
        {
            var delegateBuilder = baseNode.Bool("alwaysHandleSupSub") &&
                (options.Style.Size == Style.DISPLAY.Size || baseNode.Bool("limits"));
            return delegateBuilder
                ? (Func<ParseNode, Options, VirtualNode>)OperatornameFunctions.HtmlBuilder
                : null;
        }
        else if (baseNode.Type == "accent")
        {
            return Utils.IsCharacterBox(baseNode.Node("base"))
                ? (Func<ParseNode, Options, VirtualNode>)AccentFunctions.HtmlBuilder
                : null;
        }
        else if (baseNode.Type == "horizBrace")
        {
            var isSup = group.Node("sub") == null;
            return isSup == baseNode.Bool("isOver")
                ? (Func<ParseNode, Options, VirtualNode>)HorizBraceFunctions.HtmlBuilder
                : null;
        }
        else
        {
            return null;
        }
    }

    private static double BaseItalic(HtmlDomNode node)
    {
        if (node is SymbolNode symbol) return symbol.Italic;
        if (node is Span span) return span.Italic ?? 0;
        return 0;
    }

    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        // Superscript and subscripts are handled in the TeXbook on page
        // 445-446, rules 18(a-f).

        // Here is where we defer to the inner group if it should handle
        // superscripts and subscripts itself.
        var builderDelegate = HtmlBuilderDelegate(group, options);
        if (builderDelegate != null)
        {
            return builderDelegate(group, options);
        }

        var valueBase = group.Node("base");
        var valueSup = group.Node("sup");
        var valueSub = group.Node("sub");
        var baseNode = BuildHTML.BuildGroup(valueBase, options);
        HtmlDomNode supm = null;
        HtmlDomNode subm = null;

        var metrics = options.FontMetrics();

        // Rule 18a
        double supShift = 0;
        double subShift = 0;

        var isCharBox = valueBase != null && Utils.IsCharacterBox(valueBase);
        if (valueSup != null)
        {
            var newOptions = options.HavingStyle(options.Style.Sup());
            supm = BuildHTML.BuildGroup(valueSup, newOptions, options);
            if (!isCharBox)
            {
                supShift = baseNode.Height - newOptions.FontMetrics().SupDrop
                    * newOptions.SizeMultiplier / options.SizeMultiplier;
            }
        }

        if (valueSub != null)
        {
            var newOptions = options.HavingStyle(options.Style.Sub());
            subm = BuildHTML.BuildGroup(valueSub, newOptions, options);
            if (!isCharBox)
            {
                subShift = baseNode.Depth + newOptions.FontMetrics().SubDrop
                    * newOptions.SizeMultiplier / options.SizeMultiplier;
            }
        }

        // Rule 18c
        double minSupShift;
        if (ReferenceEquals(options.Style, Style.DISPLAY))
        {
            minSupShift = metrics.Sup1;
        }
        else if (options.Style.Cramped)
        {
            minSupShift = metrics.Sup3;
        }
        else
        {
            minSupShift = metrics.Sup2;
        }

        // scriptspace is a font-size-independent size, so scale it
        // appropriately for use as the marginRight.
        var multiplier = options.SizeMultiplier;
        var marginRight = Units.MakeEm((0.5 / metrics.PtPerEm) / multiplier);

        string marginLeft = null;
        if (subm != null)
        {
            // Subscripts shouldn't be shifted by the base's italic correction.
            // Account for that by shifting the subscript back the appropriate
            // amount. Note we only do this when the base is a single symbol.
            var opBase = group.Node("base");
            var isOiint =
                opBase != null && opBase.Type == "op" && opBase.Str("name") != null &&
                (opBase.Str("name") == "\\oiint" || opBase.Str("name") == "\\oiiint");
            if (baseNode is SymbolNode || isOiint)
            {
                // SymbolNode has .italic natively; for \oiint/\oiiint the
                // op builder stores .italic on the wrapping Span.
                marginLeft = Units.MakeEm(-BaseItalic(baseNode));
            }
        }

        Span supsub;
        if (supm != null && subm != null)
        {
            supShift = Math.Max(
                Math.Max(supShift, minSupShift), supm.Depth + 0.25 * metrics.XHeight);
            subShift = Math.Max(subShift, metrics.Sub2);

            var ruleWidth = metrics.DefaultRuleThickness;

            // Rule 18e
            var maxWidth = 4 * ruleWidth;
            if ((supShift - supm.Depth) - (subm.Height - subShift) < maxWidth)
            {
                subShift = maxWidth - (supShift - supm.Depth) + subm.Height;
                var psi = 0.8 * metrics.XHeight - (supShift - supm.Depth);
                if (psi > 0)
                {
                    supShift += psi;
                    subShift -= psi;
                }
            }

            var vlistElem = new List<VListElem>
            {
                new()
                {
                    Elem = subm, Shift = subShift, MarginRight = marginRight,
                    MarginLeft = marginLeft,
                },
                new()
                {
                    Elem = supm, Shift = -supShift, MarginRight = marginRight,
                },
            };

            supsub = BuildCommon.MakeVList(VListParam.IndividualShift(vlistElem), options);
        }
        else if (subm != null)
        {
            // Rule 18b
            subShift = Math.Max(
                Math.Max(subShift, metrics.Sub1),
                subm.Height - 0.8 * metrics.XHeight);

            var vlistElem = new List<VListElem>
            {
                new() { Elem = subm, MarginLeft = marginLeft, MarginRight = marginRight },
            };

            supsub = BuildCommon.MakeVList(VListParam.Shift(subShift, vlistElem.Cast<object>().ToList()),
                options);
        }
        else if (supm != null)
        {
            // Rule 18c, d
            supShift = Math.Max(Math.Max(supShift, minSupShift),
                supm.Depth + 0.25 * metrics.XHeight);

            supsub = BuildCommon.MakeVList(VListParam.Shift(-supShift, new List<object>
            {
                new VListElem { Elem = supm, MarginRight = marginRight },
            }), options);
        }
        else
        {
            throw new InvalidOperationException("supsub must have either sup or sub.");
        }

        // Wrap the supsub vlist in a span.msupsub to reset text-align.
        var mclass = BuildHTML.GetTypeOfDomTree(baseNode, "right") ?? "mord";
        return BuildCommon.MakeSpan(new List<string> { mclass },
            new List<VirtualNode>
            {
                baseNode,
                BuildCommon.MakeSpan(new List<string> { "msupsub" },
                    new List<VirtualNode> { supsub }),
            },
            options);
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        // Is the inner group a relevant horizontal brace?
        var isBrace = false;
        bool isOver = false;
        bool isSup = false;

        var baseNode = group.Node("base");
        if (baseNode != null && baseNode.Type == "horizBrace")
        {
            isSup = group.Node("sup") != null;
            if (isSup == baseNode.Bool("isOver"))
            {
                isBrace = true;
                isOver = baseNode.Bool("isOver");
            }
        }

        if (baseNode != null &&
            (baseNode.Type == "op" || baseNode.Type == "operatorname"))
        {
            baseNode["parentIsSupSub"] = true;
        }

        var children = new List<VirtualNode> { BuildMathML.BuildGroup(baseNode, options) };

        if (group.Node("sub") != null)
        {
            children.Add(BuildMathML.BuildGroup(group.Node("sub"), options));
        }

        if (group.Node("sup") != null)
        {
            children.Add(BuildMathML.BuildGroup(group.Node("sup"), options));
        }

        string nodeType;
        if (isBrace)
        {
            nodeType = (isOver ? "mover" : "munder");
        }
        else if (group.Node("sub") == null)
        {
            var opBase = group.Node("base");
            if (opBase != null && opBase.Type == "op" && opBase.Bool("limits") &&
                (ReferenceEquals(options.Style, Style.DISPLAY) || opBase.Bool("alwaysHandleSupSub")))
            {
                nodeType = "mover";
            }
            else if (opBase != null && opBase.Type == "operatorname" &&
                     opBase.Bool("alwaysHandleSupSub") &&
                     (opBase.Bool("limits") || ReferenceEquals(options.Style, Style.DISPLAY)))
            {
                nodeType = "mover";
            }
            else
            {
                nodeType = "msup";
            }
        }
        else if (group.Node("sup") == null)
        {
            var opBase = group.Node("base");
            if (opBase != null && opBase.Type == "op" && opBase.Bool("limits") &&
                (ReferenceEquals(options.Style, Style.DISPLAY) || opBase.Bool("alwaysHandleSupSub")))
            {
                nodeType = "munder";
            }
            else if (opBase != null && opBase.Type == "operatorname" &&
                     opBase.Bool("alwaysHandleSupSub") &&
                     (opBase.Bool("limits") || ReferenceEquals(options.Style, Style.DISPLAY)))
            {
                nodeType = "munder";
            }
            else
            {
                nodeType = "msub";
            }
        }
        else
        {
            var opBase = group.Node("base");
            if (opBase != null && opBase.Type == "op" && opBase.Bool("limits") &&
                ReferenceEquals(options.Style, Style.DISPLAY))
            {
                nodeType = "munderover";
            }
            else if (opBase != null && opBase.Type == "operatorname" &&
                     opBase.Bool("alwaysHandleSupSub") &&
                     (ReferenceEquals(options.Style, Style.DISPLAY) || opBase.Bool("limits")))
            {
                nodeType = "munderover";
            }
            else
            {
                nodeType = "msubsup";
            }
        }

        return new MathNode(nodeType, children);
    }

    public static void Define()
    {
        Registries.DefineFunctionBuilders("supsub", HtmlBuilder, MathmlBuilder);
    }
}

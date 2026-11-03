namespace KStyleMath;

public static class ArrowFunctions
{
    // Helper function
    private static MathNode PaddedNode(VirtualNode group = null)
    {
        var node = new MathNode("mpadded",
            group != null ? new List<VirtualNode> { group } : new List<VirtualNode>());
        node.SetAttribute("width", "+0.6em");
        node.SetAttribute("lspace", "0.3em");
        return node;
    }

    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        var style = options.Style;

        // Build the argument groups in the appropriate style.
        // Ref: amsmath.dtx:   \hbox{$\scriptstyle\mkern#3mu{#6}\mkern#4mu$}%

        // Some groups can return document fragments.  Handle those by wrapping
        // them in a span.
        var newOptions = options.HavingStyle(style.Sup());
        var upperGroup = BuildCommon.WrapFragment(
            BuildHTML.BuildGroup(group.Node("body"), newOptions, options), options);
        var arrowPrefix = group.Str("label").Substring(0, 2) == "\\x" ? "x" : "cd";
        upperGroup.Classes.Add(arrowPrefix + "-arrow-pad");

        HtmlDomNode lowerGroup = null;
        if (group.Node("below") != null)
        {
            // Build the lower group
            newOptions = options.HavingStyle(style.Sub());
            lowerGroup = BuildCommon.WrapFragment(
                BuildHTML.BuildGroup(group.Node("below"), newOptions, options), options);
            lowerGroup.Classes.Add(arrowPrefix + "-arrow-pad");
        }

        var arrowBody = Stretchy.StretchySvg(group, options);

        // Re shift: Note that stretchySvg returned arrowBody.depth = 0.
        // The point we want on the math axis is at 0.5 * arrowBody.height.
        var arrowShift = -options.FontMetrics().AxisHeight +
            0.5 * arrowBody.Height;
        // 2 mu kern. Ref: amsmath.dtx: #7\if0#2\else\mkern#2mu\fi
        var upperShift = -options.FontMetrics().AxisHeight
            - 0.5 * arrowBody.Height - 0.111; // 0.111 em = 2 mu
        if (upperGroup.Depth > 0.25 || group.Str("label") == "\\xleftequilibrium")
        {
            upperShift -= upperGroup.Depth;  // shift up if depth encroaches
        }

        // Generate the vlist
        Span vlist;
        if (lowerGroup != null)
        {
            var lowerShift = -options.FontMetrics().AxisHeight
                + lowerGroup.Height + 0.5 * arrowBody.Height
                + 0.111;
            vlist = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                new() { Elem = upperGroup, Shift = upperShift },
                new()
                {
                    Elem = arrowBody,
                    Shift = arrowShift,
                    WrapperClasses = new List<string> { "svg-align" },
                },
                new() { Elem = lowerGroup, Shift = lowerShift },
            }), options);
        }
        else
        {
            vlist = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                new() { Elem = upperGroup, Shift = upperShift },
                new()
                {
                    Elem = arrowBody,
                    Shift = arrowShift,
                    WrapperClasses = new List<string> { "svg-align" },
                },
            }), options);
        }

        return BuildCommon.MakeSpan(new List<string> { "mrel", "x-arrow" },
            new List<VirtualNode> { vlist }, options);
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var arrowNode = Stretchy.StretchyMathML(group.Str("label"));
        arrowNode.SetAttribute(
            "minsize", group.Str("label")[0] == 'x' ? "1.75em" : "3.0em"
        );
        VirtualNode node;

        if (group.Node("body") != null)
        {
            var upperNode = PaddedNode(BuildMathML.BuildGroup(group.Node("body"), options));
            if (group.Node("below") != null)
            {
                var lowerNode = PaddedNode(BuildMathML.BuildGroup(group.Node("below"), options));
                node = new MathNode(
                    "munderover", new List<VirtualNode> { arrowNode, lowerNode, upperNode }
                );
            }
            else
            {
                node = new MathNode("mover", new List<VirtualNode> { arrowNode, upperNode });
            }
        }
        else if (group.Node("below") != null)
        {
            var lowerNode = PaddedNode(BuildMathML.BuildGroup(group.Node("below"), options));
            node = new MathNode("munder", new List<VirtualNode> { arrowNode, lowerNode });
        }
        else
        {
            // This should never happen.
            // Parser.js throws an error if there is no argument.
            node = PaddedNode();
            node = new MathNode("mover", new List<VirtualNode> { arrowNode, node });
        }
        return node;
    }

    public static void Define()
    {
        // Stretchy arrows with an optional argument
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "xArrow",
            Names = new List<string>
            {
                "\\xleftarrow", "\\xrightarrow", "\\xLeftarrow", "\\xRightarrow",
                "\\xleftrightarrow", "\\xLeftrightarrow", "\\xhookleftarrow",
                "\\xhookrightarrow", "\\xmapsto", "\\xrightharpoondown",
                "\\xrightharpoonup", "\\xleftharpoondown", "\\xleftharpoonup",
                "\\xrightleftharpoons", "\\xleftrightharpoons", "\\xlongequal",
                "\\xtwoheadrightarrow", "\\xtwoheadleftarrow", "\\xtofrom",
                // The next 3 functions are here to support the mhchem extension.
                // Direct use of these functions is discouraged and may break someday.
                "\\xrightleftarrows", "\\xrightequilibrium", "\\xleftequilibrium",
                // The next 3 functions are here only to support the {CD} environment.
                "\\\\cdrightarrow", "\\\\cdleftarrow", "\\\\cdlongequal",
            },
            NumArgs = 1,
            NumOptionalArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("xArrow", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["body"] = args.Count > 0 ? args[0] : null,
                    ["below"] = optArgs.Count > 0 ? optArgs[0] : null,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });
    }
}


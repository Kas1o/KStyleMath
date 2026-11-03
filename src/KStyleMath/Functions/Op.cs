namespace KStyleMath;

public static class OpFunctions
{
    // Most operators have a large successor symbol, but these don't.
    private static readonly HashSet<string> NoSuccessor = new(StringComparer.Ordinal)
    {
        "\\smallint",
    };

    // NOTE: Unlike most `htmlBuilder`s, this one handles not only "op", but also
    // "supsub" since some of them (like \int) can affect super/subscripting.
    public static VirtualNode HtmlBuilder(ParseNode grp, Options options)
    {
        // Operators are handled in the TeXbook pg. 443-444, rule 13(a).
        ParseNode supGroup = null;
        ParseNode subGroup = null;
        var hasLimits = false;
        ParseNode group;
        if (grp.Type == "supsub")
        {
            // If we have limits, supsub will pass us its group to handle. Pull
            // out the superscript and subscript and set the group to the op in
            // its base.
            supGroup = grp.Node("sup");
            subGroup = grp.Node("sub");
            group = ParseNodeUtil.AssertNodeType(grp.Node("base"), "op");
            hasLimits = true;
        }
        else
        {
            group = ParseNodeUtil.AssertNodeType(grp, "op");
        }

        var style = options.Style;

        var large = false;
        if (style.Size == Style.DISPLAY.Size &&
            group.Bool("symbol") &&
            !NoSuccessor.Contains(group.Str("name")))
        {
            // Most symbol operators get larger in displaystyle (rule 13)
            large = true;
        }

        HtmlDomNode baseNode;
        // Italic correction from the symbol glyph, captured before the symbol
        // may be wrapped in a vlist (for \oiint/\oiiint).  Stays 0 for non-symbol ops.
        double symbolItalic = 0;
        if (group.Bool("symbol"))
        {
            // If this is a symbol, create the symbol.
            var fontName = large ? "Size2-Regular" : "Size1-Regular";

            var stash = "";
            if (group.Str("name") == "\\oiint" || group.Str("name") == "\\oiiint")
            {
                // No font glyphs yet, so use a glyph w/o the oval.
                // TODO: When font glyphs are available, delete this code.
                stash = group.Str("name").Substring(1);
                group["name"] = stash == "oiint" ? "\\iint" : "\\iiint";
            }

            var symbol = BuildCommon.MakeSymbol(
                group.Str("name"), fontName, "math", options,
                new List<string> { "mop", "op-symbol", large ? "large-op" : "small-op" });
            symbolItalic = symbol.Italic;
            baseNode = symbol;

            if (stash.Length > 0)
            {
                // We're in \oiint or \oiiint. Overlay the oval.
                // TODO: When font glyphs are available, delete this code.
                var oval = BuildCommon.StaticSvg(stash + "Size"
                    + (large ? "2" : "1"), options);
                baseNode = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
                {
                    new() { Elem = baseNode, Shift = 0 },
                    new() { Elem = oval, Shift = large ? 0.08 : 0 },
                }), options);
                group["name"] = "\\" + stash;
                baseNode.Classes.Insert(0, "mop");
                // Carry the italic correction from the original symbol to the
                // vlist wrapper so supsub can use it for subscript positioning.
                ((Span)baseNode).Italic = symbolItalic;
            }
        }
        else if (group.List("body") != null)
        {
            // If this is a list, compose that list.
            var inner = BuildHTML.BuildExpression(group.List("body"), options, true);
            if (inner.Count == 1 && inner[0] is SymbolNode symbol)
            {
                baseNode = symbol;
                baseNode.Classes[0] = "mop"; // replace old mclass
            }
            else
            {
                baseNode = BuildCommon.MakeSpan(new List<string> { "mop" },
                    inner.Cast<VirtualNode>().ToList(), options);
            }
        }
        else
        {
            // Otherwise, this is a text operator. Build the text from the
            // operator's name.
            var output = new List<VirtualNode>();
            var name = group.Str("name");
            for (var i = 1; i < name.Length; i++)
            {
                output.Add(BuildCommon.Mathsym(name[i].ToString(), group.Mode, options));
            }
            baseNode = BuildCommon.MakeSpan(new List<string> { "mop" }, output, options);
        }

        // If content of op is a single symbol, shift it vertically.
        double baseShift = 0;
        double slant = 0;
        if ((baseNode is SymbolNode
             || group.Str("name") == "\\oiint" || group.Str("name") == "\\oiiint")
            && !group.Bool("suppressBaseShift"))
        {
            // We suppress the shift of the base of \overset and \underset. Otherwise,
            // shift the symbol so its center lies on the axis (rule 13). It
            // appears that our fonts have the centers of the symbols already
            // almost on the axis, so these numbers are very small. Note we
            // don't actually apply this here, but instead it is used either in
            // the vlist creation or separately when there are no limits.
            baseShift = (baseNode.Height - baseNode.Depth) / 2 -
                options.FontMetrics().AxisHeight;

            // The slant of the symbol is just its italic correction.
            // SymbolNode carries .italic natively; Span (for \oiint/\oiiint)
            // only has it set when nonzero, so default to 0.
            slant = baseNode is SymbolNode baseSymbol
                ? baseSymbol.Italic
                : baseNode is Span baseSpan ? baseSpan.Italic ?? 0 : 0;
        }

        if (hasLimits)
        {
            return AssembleSupSub.AssembleSupSubFunc(baseNode, supGroup, subGroup, options,
                style, slant, baseShift);
        }
        else
        {
            if (baseShift != 0)
            {
                baseNode.Style.Position = "relative";
                baseNode.Style.Top = Units.MakeEm(baseShift);
            }

            return baseNode;
        }
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        VirtualNode node;

        if (group.Bool("symbol"))
        {
            // This is a symbol. Just add the symbol.
            node = new MathNode(
                "mo", new List<VirtualNode> { BuildMathML.MakeText(group.Str("name"), group.Mode) });
            if (NoSuccessor.Contains(group.Str("name")))
            {
                ((MathNode)node).SetAttribute("largeop", "false");
            }
        }
        else if (group.List("body") != null)
        {
            // This is an operator with children. Add them.
            node = new MathNode(
                "mo", BuildMathML.BuildExpression(group.List("body"), options)
                    .Cast<VirtualNode>().ToList());
        }
        else
        {
            // This is a text operator. Add all the characters from the
            // operator's name.
            node = new MathNode(
                "mi", new List<VirtualNode> { new MathTextNode(group.Str("name").Substring(1)) });
            // Append an <mo>&ApplyFunction;</mo>.
            // ref: https://www.w3.org/TR/REC-MathML/chap3_2.html#sec3.2.4
            var op = new MathNode("mo",
                new List<VirtualNode> { BuildMathML.MakeText("\u2061", "text") });
            if (group.Bool("parentIsSupSub"))
            {
                node = new MathNode("mrow", new List<VirtualNode> { node, op });
            }
            else
            {
                node = MathMLTree.NewDocumentFragment(new List<VirtualNode> { node, op });
            }
        }

        return node;
    }

    private static readonly Dictionary<string, string> SingleCharBigOps = new(StringComparer.Ordinal)
    {
        ["\u220F"] = "\\prod",
        ["\u2210"] = "\\coprod",
        ["\u2211"] = "\\sum",
        ["\u22c0"] = "\\bigwedge",
        ["\u22c1"] = "\\bigvee",
        ["\u22c2"] = "\\bigcap",
        ["\u22c3"] = "\\bigcup",
        ["\u2a00"] = "\\bigodot",
        ["\u2a01"] = "\\bigoplus",
        ["\u2a02"] = "\\bigotimes",
        ["\u2a04"] = "\\biguplus",
        ["\u2a06"] = "\\bigsqcup",
    };

    private static readonly Dictionary<string, string> SingleCharIntegrals = new(StringComparer.Ordinal)
    {
        ["\u222b"] = "\\int",
        ["\u222c"] = "\\iint",
        ["\u222d"] = "\\iiint",
        ["\u222e"] = "\\oint",
        ["\u222f"] = "\\oiint",
        ["\u2230"] = "\\oiiint",
    };

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "op",
            Names = new List<string>
            {
                "\\coprod", "\\bigvee", "\\bigwedge", "\\biguplus", "\\bigcap",
                "\\bigcup", "\\intop", "\\prod", "\\sum", "\\bigotimes",
                "\\bigoplus", "\\bigodot", "\\bigsqcup", "\\smallint", "\u220F",
                "\u2210", "\u2211", "\u22c0", "\u22c1", "\u22c2", "\u22c3", "\u2a00",
                "\u2a01", "\u2a02", "\u2a04", "\u2a06",
            },
            NumArgs = 0,

            Handler = (context, args, optArgs) =>
            {
                var fName = context.FuncName;
                if (fName.Length == 1)
                {
                    fName = SingleCharBigOps[fName];
                }
                return new ParseNode("op", context.Parser.Mode)
                {
                    ["limits"] = true,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = true,
                    ["name"] = fName,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "op",
            Names = new List<string> { "\\mathop" },
            NumArgs = 1,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("op", context.Parser.Mode)
                {
                    ["limits"] = false,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = false,
                    ["body"] = Registries.Ordargument(body),
                };
            },
        });

        // There are 2 flags for operators; whether they produce limits in
        // displaystyle, and whether they are symbols and should grow in
        // displaystyle. These four groups cover the four possible choices.

        // No limits, not symbols
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "op",
            Names = new List<string>
            {
                "\\arcsin", "\\arccos", "\\arctan", "\\arctg", "\\arcctg",
                "\\arg", "\\ch", "\\cos", "\\cosec", "\\cosh", "\\cot", "\\cotg",
                "\\coth", "\\csc", "\\ctg", "\\cth", "\\deg", "\\dim", "\\exp",
                "\\hom", "\\ker", "\\lg", "\\ln", "\\log", "\\sec", "\\sin",
                "\\sinh", "\\sh", "\\tan", "\\tanh", "\\tg", "\\th",
            },
            NumArgs = 0,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("op", context.Parser.Mode)
                {
                    ["limits"] = false,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = false,
                    ["name"] = context.FuncName,
                };
            },
        });

        // Limits, not symbols
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "op",
            Names = new List<string>
            {
                "\\det", "\\gcd", "\\inf", "\\lim", "\\max", "\\min", "\\Pr", "\\sup",
            },
            NumArgs = 0,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("op", context.Parser.Mode)
                {
                    ["limits"] = true,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = false,
                    ["name"] = context.FuncName,
                };
            },
        });

        // No limits, symbols
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "op",
            Names = new List<string>
            {
                "\\int", "\\iint", "\\iiint", "\\oint", "\\oiint", "\\oiiint",
                "\u222b", "\u222c", "\u222d", "\u222e", "\u222f", "\u2230",
            },
            NumArgs = 0,
            AllowedInArgument = true,

            Handler = (context, args, optArgs) =>
            {
                var fName = context.FuncName;
                if (fName.Length == 1)
                {
                    fName = SingleCharIntegrals[fName];
                }
                return new ParseNode("op", context.Parser.Mode)
                {
                    ["limits"] = false,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = true,
                    ["name"] = fName,
                };
            },
        });
    }
}


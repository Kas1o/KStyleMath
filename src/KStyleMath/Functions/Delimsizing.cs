using System.Runtime.CompilerServices;

namespace KStyleMath;

public static class DelimsizingFunctions
{
    // Extra data needed for the delimiter handler down below
    private static readonly Dictionary<string, (string Mclass, int Size)> DelimiterSizes =
        new(StringComparer.Ordinal)
        {
            ["\\bigl"] = ("mopen", 1),
            ["\\Bigl"] = ("mopen", 2),
            ["\\biggl"] = ("mopen", 3),
            ["\\Biggl"] = ("mopen", 4),
            ["\\bigr"] = ("mclose", 1),
            ["\\Bigr"] = ("mclose", 2),
            ["\\biggr"] = ("mclose", 3),
            ["\\Biggr"] = ("mclose", 4),
            ["\\bigm"] = ("mrel", 1),
            ["\\Bigm"] = ("mrel", 2),
            ["\\biggm"] = ("mrel", 3),
            ["\\Biggm"] = ("mrel", 4),
            ["\\big"] = ("mord", 1),
            ["\\Big"] = ("mord", 2),
            ["\\bigg"] = ("mord", 3),
            ["\\Bigg"] = ("mord", 4),
        };

    private static readonly HashSet<string> Delimiters = new(StringComparer.Ordinal)
    {
        "(", "\\lparen", ")", "\\rparen",
        "[", "\\lbrack", "]", "\\rbrack",
        "\\{", "\\lbrace", "\\}", "\\rbrace",
        "\\lfloor", "\\rfloor", "\u230a", "\u230b",
        "\\lceil", "\\rceil", "\u2308", "\u2309",
        "<", ">", "\\langle", "\u27e8", "\\rangle", "\u27e9", "\\lt", "\\gt",
        "\\lvert", "\\rvert", "\\lVert", "\\rVert",
        "\\lgroup", "\\rgroup", "\u27ee", "\u27ef",
        "\\lmoustache", "\\rmoustache", "\u23b0", "\u23b1",
        "/", "\\backslash",
        "|", "\\vert", "\\|", "\\Vert",
        "\\uparrow", "\\Uparrow",
        "\\downarrow", "\\Downarrow",
        "\\updownarrow", "\\Updownarrow",
        ".",
    };

    private sealed class MiddleInfo
    {
        public string Delim;
        public Options Options;
    }

    // An HtmlDomNode that carries an `isMiddle` property, used by the
    // \middle command to communicate delimiter info to the \left/\right builder.
    private static readonly ConditionalWeakTable<VirtualNode, MiddleInfo> MiddleNodes = new();

    private static bool IsMiddleDelimNode(HtmlDomNode node)
        => MiddleNodes.TryGetValue(node, out _);

    // Delimiter functions
    private static ParseNode CheckDelimiter(ParseNode delim, FunctionContext context)
    {
        var symDelim = ParseNodeUtil.CheckSymbolNodeType(delim);
        if (symDelim != null && Delimiters.Contains(symDelim.Str("text")))
        {
            return symDelim;
        }
        else if (symDelim != null)
        {
            throw new ParseError(
                $"Invalid delimiter '{symDelim.Str("text")}' after '{context.FuncName}'",
                delim);
        }
        else
        {
            throw new ParseError($"Invalid delimiter type '{delim.Type}'", delim);
        }
    }

    private static void AssertParsed(ParseNode group)
    {
        if (group.List("body") == null)
        {
            throw new InvalidOperationException("Bug: The leftright ParseNode wasn't fully parsed.");
        }
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "delimsizing",
            Names = new List<string>
            {
                "\\bigl", "\\Bigl", "\\biggl", "\\Biggl",
                "\\bigr", "\\Bigr", "\\biggr", "\\Biggr",
                "\\bigm", "\\Bigm", "\\biggm", "\\Biggm",
                "\\big", "\\Big", "\\bigg", "\\Bigg",
            },
            NumArgs = 1,
            ArgTypes = new List<string> { "primitive" },

            Handler = (context, args, optArgs) =>
            {
                var delim = CheckDelimiter(Registries.NormalizeArgument(args[0]), context);
                var size = DelimiterSizes[context.FuncName];

                return new ParseNode("delimsizing", context.Parser.Mode)
                {
                    ["size"] = (double)size.Size,
                    ["mclass"] = size.Mclass,
                    ["delim"] = delim.Str("text"),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                if (group.Str("delim") == ".")
                {
                    // Empty delimiters still count as elements, even though they don't
                    // show anything.
                    return BuildCommon.MakeSpan(new List<string> { group.Str("mclass") });
                }

                return Delimiter.MakeSizedDelim(
                    group.Str("delim"), (int)group.Num("size"), options, group.Mode,
                    new List<string> { group.Str("mclass") });
            },
            MathmlBuilder = (group, options) =>
            {
                var children = new List<VirtualNode>();

                if (group.Str("delim") != ".")
                {
                    children.Add(BuildMathML.MakeText(group.Str("delim"), group.Mode));
                }

                var node = new MathNode("mo", children);

                if (group.Str("mclass") == "mopen" ||
                    group.Str("mclass") == "mclose")
                {
                    // Only some of the delimsizing functions act as fences, and they
                    // return "mopen" or "mclose" mclass.
                    node.SetAttribute("fence", "true");
                }
                else
                {
                    // Explicitly disable fencing if it's not a fence, to override the
                    // defaults.
                    node.SetAttribute("fence", "false");
                }

                node.SetAttribute("stretchy", "true");
                var size = Units.MakeEm(Delimiter.SizeToMaxHeight[(int)group.Num("size")]);
                node.SetAttribute("minsize", size);
                node.SetAttribute("maxsize", size);

                return node;
            },
        });


        Registries.DefineFunction(new FunctionSpec
        {
            Type = "leftright-right",
            Names = new List<string> { "\\right" },
            NumArgs = 1,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                // \left case below triggers parsing of \right in
                //   `const right = parser.parseFunction();`
                // uses this return value.
                var colorObj = context.Parser.Gullet.Macros.Get("\\current@color");
                if (colorObj != null && colorObj is not string)
                {
                    throw new ParseError(
                        "\\current@color set to non-string in \\right");
                }
                var color = colorObj as string;
                return new ParseNode("leftright-right", context.Parser.Mode)
                {
                    ["delim"] = CheckDelimiter(args[0], context).Str("text"),
                    ["color"] = color, // null if not set via \color
                };
            },
        });


        Registries.DefineFunction(new FunctionSpec
        {
            Type = "leftright",
            Names = new List<string> { "\\left" },
            NumArgs = 1,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var delim = CheckDelimiter(args[0], context);

                var parser = context.Parser;
                // Parse out the implicit body
                ++parser.LeftrightDepth;
                // parseExpression stops before '\\right'
                var body = parser.ParseExpression(false);
                --parser.LeftrightDepth;
                // Check the next token
                parser.Expect("\\right", false);
                var right = ParseNodeUtil.AssertNodeType(parser.ParseFunction(), "leftright-right");
                return new ParseNode("leftright", parser.Mode)
                {
                    ["body"] = body,
                    ["left"] = delim.Str("text"),
                    ["right"] = right.Str("delim"),
                    ["rightColor"] = right.Get<string>("color"),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                AssertParsed(group);
                // Build the inner expression
                var inner = BuildHTML.BuildExpression(group.List("body"), options, true,
                    new[] { "mopen", "mclose" });

                double innerHeight = 0;
                double innerDepth = 0;
                var hadMiddle = false;

                // Calculate its height and depth
                for (var i = 0; i < inner.Count; i++)
                {
                    var node = inner[i];
                    if (IsMiddleDelimNode(node))
                    {
                        hadMiddle = true;
                    }
                    else
                    {
                        innerHeight = Math.Max(inner[i].Height, innerHeight);
                        innerDepth = Math.Max(inner[i].Depth, innerDepth);
                    }
                }

                // The size of delimiters is the same, regardless of what style we are
                // in. Thus, to correctly calculate the size of delimiter we need around
                // a group, we scale down the inner size based on the size.
                innerHeight *= options.SizeMultiplier;
                innerDepth *= options.SizeMultiplier;

                HtmlDomNode leftDelim;
                if (group.Str("left") == ".")
                {
                    // Empty delimiters in \left and \right make null delimiter spaces.
                    leftDelim = BuildHTML.MakeNullDelimiter(options, new List<string> { "mopen" });
                }
                else
                {
                    // Otherwise, use leftRightDelim to generate the correct sized
                    // delimiter.
                    leftDelim = Delimiter.MakeLeftRightDelim(
                        group.Str("left"), innerHeight, innerDepth, options,
                        group.Mode, new List<string> { "mopen" });
                }
                // Add it to the beginning of the expression
                inner.Insert(0, leftDelim);

                // Handle middle delimiters
                if (hadMiddle)
                {
                    for (var i = 1; i < inner.Count; i++)
                    {
                        var middleDelim = inner[i];
                        if (IsMiddleDelimNode(middleDelim))
                        {
                            MiddleNodes.TryGetValue(middleDelim, out var isMiddle);
                            // Apply the options that were active when \middle was called
                            inner[i] = Delimiter.MakeLeftRightDelim(
                                isMiddle.Delim, innerHeight, innerDepth,
                                isMiddle.Options, group.Mode, new List<string>());
                        }
                    }
                }

                HtmlDomNode rightDelim;
                // Same for the right delimiter, but using color specified by \color
                if (group.Str("right") == ".")
                {
                    rightDelim = BuildHTML.MakeNullDelimiter(options, new List<string> { "mclose" });
                }
                else
                {
                    var rightColor = group.Get<string>("rightColor");
                    var colorOptions = rightColor != null ?
                        options.WithColor(rightColor) : options;
                    rightDelim = Delimiter.MakeLeftRightDelim(
                        group.Str("right"), innerHeight, innerDepth, colorOptions,
                        group.Mode, new List<string> { "mclose" });
                }
                // Add it to the end of the expression.
                inner.Add(rightDelim);

                return BuildCommon.MakeSpan(new List<string> { "minner" },
                    inner.Cast<VirtualNode>().ToList(), options);
            },
            MathmlBuilder = (group, options) =>
            {
                AssertParsed(group);
                var inner = BuildMathML.BuildExpression(group.List("body"), options);

                if (group.Str("left") != ".")
                {
                    var leftNode = new MathNode(
                        "mo", new List<VirtualNode> { BuildMathML.MakeText(group.Str("left"), group.Mode) });

                    leftNode.SetAttribute("fence", "true");

                    inner.Insert(0, leftNode);
                }

                if (group.Str("right") != ".")
                {
                    var rightNode = new MathNode(
                        "mo", new List<VirtualNode> { BuildMathML.MakeText(group.Str("right"), group.Mode) });

                    rightNode.SetAttribute("fence", "true");

                    var rightColor = group.Get<string>("rightColor");
                    if (rightColor != null)
                    {
                        rightNode.SetAttribute("mathcolor", rightColor);
                    }

                    inner.Add(rightNode);
                }

                return BuildMathML.MakeRow(inner.Cast<VirtualNode>().ToList());
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "middle",
            Names = new List<string> { "\\middle" },
            NumArgs = 1,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var delim = CheckDelimiter(args[0], context);
                if (context.Parser.LeftrightDepth == 0)
                {
                    throw new ParseError("\\middle without preceding \\left", delim);
                }

                return new ParseNode("middle", context.Parser.Mode)
                {
                    ["delim"] = delim.Str("text"),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                HtmlDomNode middleDelim;
                if (group.Str("delim") == ".")
                {
                    middleDelim = BuildHTML.MakeNullDelimiter(options, new List<string>());
                }
                else
                {
                    middleDelim = Delimiter.MakeSizedDelim(
                        group.Str("delim"), 1, options,
                        group.Mode, new List<string>());

                    // Patch an ad-hoc property onto the node so the \left/\right
                    // builder can reconstruct appropriately sized middle delimiters.
                    MiddleNodes.Add(middleDelim, new MiddleInfo
                    {
                        Delim = group.Str("delim"),
                        Options = options,
                    });
                }
                return middleDelim;
            },
            MathmlBuilder = (group, options) =>
            {
                // A Firefox \middle will stretch a character vertically only if it
                // is in the fence part of the operator dictionary at:
                // https://www.w3.org/TR/MathML3/appendixc.html.
                // So we need to avoid U+2223 and use plain "|" instead.
                var textNode = (group.Str("delim") == "\\vert" || group.Str("delim") == "|")
                    ? BuildMathML.MakeText("|", "text")
                    : BuildMathML.MakeText(group.Str("delim"), group.Mode);
                var middleNode = new MathNode("mo", new List<VirtualNode> { textNode });
                middleNode.SetAttribute("fence", "true");
                // MathML gives 5/18em spacing to each <mo> element.
                // \middle should get delimiter spacing instead.
                middleNode.SetAttribute("lspace", "0.05em");
                middleNode.SetAttribute("rspace", "0.05em");
                return middleNode;
            },
        });
    }
}



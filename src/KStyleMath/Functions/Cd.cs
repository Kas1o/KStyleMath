namespace KStyleMath;

/// <summary>Port of .katex-ref/src/environments/cd.ts.</summary>
public static class CdFunctions
{
    private static readonly Dictionary<string, string> CdArrowFunctionName = new(StringComparer.Ordinal)
    {
        [">"] = "\\\\cdrightarrow",
        ["<"] = "\\\\cdleftarrow",
        ["="] = "\\\\cdlongequal",
        ["A"] = "\\uparrow",
        ["V"] = "\\downarrow",
        ["|"] = "\\Vert",
        ["."] = "no arrow",
    };

    private static ParseNode NewCell()
    {
        // Create an empty cell, to be filled below with parse nodes.
        // The parseTree from this module must be constructed like the
        // one created by parseArray(), so an empty CD cell must
        // be a ParseNode<"styling">. And CD is always displaystyle.
        return new ParseNode("styling", "math")
        {
            ["body"] = new List<ParseNode>(),
            ["style"] = "display",
            ["resetFont"] = true,
        };
    }

    private static bool IsStartOfArrow(ParseNode node)
    {
        return node.Type == "textord" && node.Str("text") == "@";
    }

    private static bool IsLabelEnd(ParseNode node, string endChar)
    {
        return (node.Type == "mathord" || node.Type == "atom") &&
            node.Str("text") == endChar;
    }

    private static ParseNode CdArrow(string arrowChar, List<ParseNode> labels, Parser parser)
    {
        // Return a parse tree of an arrow and its labels.
        // This acts in a way similar to a macro expansion.
        var funcName = CdArrowFunctionName[arrowChar];
        switch (funcName)
        {
            case "\\\\cdrightarrow":
            case "\\\\cdleftarrow":
                return parser.CallFunction(
                    funcName, new List<ParseNode> { labels[0] }, new List<ParseNode> { labels[1] });
            case "\\uparrow":
            case "\\downarrow":
            {
                var leftLabel = parser.CallFunction(
                    "\\\\cdleft", new List<ParseNode> { labels[0] }, new List<ParseNode>());
                var bareArrow = new ParseNode("atom", "math")
                {
                    ["text"] = funcName,
                    ["family"] = "rel",
                };
                var sizedArrow = parser.CallFunction("\\Big", new List<ParseNode> { bareArrow }, new List<ParseNode>());
                var rightLabel = parser.CallFunction(
                    "\\\\cdright", new List<ParseNode> { labels[1] }, new List<ParseNode>());
                var arrowGroup = new ParseNode("ordgroup", "math")
                {
                    ["body"] = new List<ParseNode> { leftLabel, sizedArrow, rightLabel },
                };
                return parser.CallFunction("\\\\cdparent", new List<ParseNode> { arrowGroup }, new List<ParseNode>());
            }
            case "\\\\cdlongequal":
                return parser.CallFunction("\\\\cdlongequal", new List<ParseNode>(), new List<ParseNode>());
            case "\\Vert":
            {
                var arrow = new ParseNode("textord", "math") { ["text"] = "\\Vert" };
                return parser.CallFunction("\\Big", new List<ParseNode> { arrow }, new List<ParseNode>());
            }
            default:
                return new ParseNode("textord", "math") { ["text"] = " " };
        }
    }

    public static ParseNode ParseCD(Parser parser)
    {
        // Get the array's parse nodes with \\ temporarily mapped to \cr.
        var parsedRows = new List<List<ParseNode>>();
        parser.Gullet.BeginGroup();
        parser.Gullet.Macros.Set("\\cr", "\\\\\\relax");
        parser.Gullet.BeginGroup();
        while (true)
        {
            // Get the parse nodes for the next row.
            parsedRows.Add(parser.ParseExpression(false, "\\\\"));
            parser.Gullet.EndGroup();
            parser.Gullet.BeginGroup();
            var next = parser.Fetch().Text;
            if (next == "&" || next == "\\\\")
            {
                parser.Consume();
            }
            else if (next == "\\end")
            {
                if (parsedRows[parsedRows.Count - 1].Count == 0)
                {
                    parsedRows.RemoveAt(parsedRows.Count - 1); // final row ended in \\
                }
                break;
            }
            else
            {
                throw new ParseError("Expected \\\\ or \\cr or \\end",
                                     parser.NextToken);
            }
        }

        var row = new List<ParseNode>();
        var body = new List<List<ParseNode>> { row };

        // Loop thru the parse nodes. Collect them into cells and arrows.
        for (var i = 0; i < parsedRows.Count; i++)
        {
            // Start a new row.
            var rowNodes = parsedRows[i];
            // Create the first cell.
            var cell = NewCell();

            for (var j = 0; j < rowNodes.Count; j++)
            {
                if (!IsStartOfArrow(rowNodes[j]))
                {
                    // If a parseNode is not an arrow, it goes into a cell.
                    cell.List("body").Add(rowNodes[j]);
                }
                else
                {
                    // Parse node j is an "@", the start of an arrow.
                    // Before starting on the arrow, push the cell into `row`.
                    row.Add(cell);

                    // Now collect parseNodes into an arrow.
                    // The character after "@" defines the arrow type.
                    j += 1;
                    var arrowChar = ParseNodeUtil.AssertSymbolNodeType(rowNodes[j]).Str("text") ?? "";

                    // Create two empty label nodes. We may or may not use them.
                    var labels = new List<ParseNode>();
                    labels.Add(new ParseNode("ordgroup", "math") { ["body"] = new List<ParseNode>() });
                    labels.Add(new ParseNode("ordgroup", "math") { ["body"] = new List<ParseNode>() });

                    // Process the arrow.
                    if ("=|.".Contains(arrowChar))
                    {
                        // Three "arrows", ``@=`, `@|`, and `@.`, do not take labels.
                        // Do nothing here.
                    }
                    else if ("<>AV".Contains(arrowChar))
                    {
                        // Four arrows, `@>>>`, `@<<<`, `@AAA`, and `@VVV`, each take
                        // two optional labels. E.g. the right-point arrow syntax is
                        // really:  @>{optional label}>{optional label}>
                        // Collect parseNodes into labels.
                        for (var labelNum = 0; labelNum < 2; labelNum++)
                        {
                            var inLabel = true;
                            for (var k = j + 1; k < rowNodes.Count; k++)
                            {
                                if (IsLabelEnd(rowNodes[k], arrowChar))
                                {
                                    inLabel = false;
                                    j = k;
                                    break;
                                }
                                if (IsStartOfArrow(rowNodes[k]))
                                {
                                    throw new ParseError("Missing a " + arrowChar +
                                    " character to complete a CD arrow.", rowNodes[k]);
                                }

                                labels[labelNum].List("body").Add(rowNodes[k]);
                            }
                            if (inLabel)
                            {
                                // isLabelEnd never returned a true.
                                throw new ParseError("Missing a " + arrowChar +
                                    " character to complete a CD arrow.", rowNodes[j]);
                            }
                        }
                    }
                    else
                    {
                        throw new ParseError($"Expected one of \"<>AV=|.\" after @",
                            rowNodes[j]);
                    }

                    // Now join the arrow to its labels.
                    var arrow = CdArrow(arrowChar, labels, parser);

                    // Wrap the arrow in  ParseNode<"styling">.
                    // This is done to match parseArray() behavior.
                    var wrappedArrow = new ParseNode("styling", "math")
                    {
                        ["body"] = new List<ParseNode> { arrow },
                        ["style"] = "display", // CD is always displaystyle.
                        ["resetFont"] = true,
                    };
                    row.Add(wrappedArrow);
                    // In CD's syntax, cells are implicit. That is, everything that
                    // is not an arrow gets collected into a cell. So create an empty
                    // cell now. It will collect upcoming parseNodes.
                    cell = NewCell();
                }
            }
            if (i % 2 == 0)
            {
                // Even-numbered rows consist of: cell, arrow, cell, arrow, ... cell
                // The last cell is not yet pushed into `row`, so:
                row.Add(cell);
            }
            else
            {
                // Odd-numbered rows consist of: vert arrow, empty cell, ... vert arrow
                // Remove the empty cell that was placed at the beginning of `row`.
                row.RemoveAt(0);
            }
            row = new List<ParseNode>();
            body.Add(row);
        }

        // End row group
        parser.Gullet.EndGroup();
        // End array group defining \\
        parser.Gullet.EndGroup();

        // define column separation.
        var colSpec = new AlignSpec
        {
            Type = "align",
            Align = "c",
            Pregap = 0.25,  // CD package sets \enskip between columns.
            Postgap = 0.25, // So pre and post each get half an \enskip, i.e. 0.25em.
        };
        var cols = Enumerable.Repeat(colSpec, body[0].Count).ToList();

        var hLinesBeforeRow = new List<List<bool>>();
        for (var i = 0; i < body.Count + 1; i++)
        {
            hLinesBeforeRow.Add(new List<bool>());
        }
        return new ParseNode("array", "math")
        {
            ["body"] = body,
            ["arraystretch"] = 1.0,
            ["addJot"] = true,
            ["rowGaps"] = new List<Measurement> { null },
            ["cols"] = cols,
            ["colSeparationType"] = "CD",
            ["hLinesBeforeRow"] = hLinesBeforeRow,
        };
    }

    // The functions below are not available for general use.
    // They are here only for internal use by the {CD} environment in placing labels
    // next to vertical arrows.

    // We don't need any such functions for horizontal arrows because we can reuse
    // the functionality that already exists for extensible arrows.
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "cdlabel",
            Names = new List<string> { "\\\\cdleft", "\\\\cdright" },
            NumArgs = 1,
            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("cdlabel", context.Parser.Mode)
                {
                    ["side"] = context.FuncName.Substring(4),
                    ["label"] = args[0],
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var newOptions = options.HavingStyle(options.Style.Sup());
                var label = BuildCommon.WrapFragment(
                    BuildHTML.BuildGroup(group.Node("label"), newOptions, options), options);
                label.Classes.Add("cd-label-" + group.Str("side"));
                label.Style.Bottom = Units.MakeEm(0.8 - label.Depth);
                // Zero out label height & depth, so vertical align of arrow is set
                // by the arrow height, not by the label.
                label.Height = 0;
                label.Depth = 0;
                return label;
            },
            MathmlBuilder = (group, options) =>
            {
                var label = new MathNode("mrow",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("label"), options) });
                label = new MathNode("mpadded", new List<VirtualNode> { label });
                label.SetAttribute("width", "0");
                if (group.Str("side") == "left")
                {
                    label.SetAttribute("lspace", "-1width");
                }
                // We have to guess at vertical alignment. We know the arrow is 1.8em tall,
                // But we don't know the height or depth of the label.
                label.SetAttribute("voffset", "0.7em");
                label = new MathNode("mstyle", new List<VirtualNode> { label });
                label.SetAttribute("displaystyle", "false");
                label.SetAttribute("scriptlevel", "1");
                return label;
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "cdlabelparent",
            Names = new List<string> { "\\\\cdparent" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("cdlabelparent", context.Parser.Mode)
                {
                    ["fragment"] = args[0],
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Wrap the vertical arrow and its labels.
                // The parent gets position: relative. The child gets position: absolute.
                // So CSS can locate the label correctly.
                var parent = BuildCommon.WrapFragment(
                    BuildHTML.BuildGroup(group.Node("fragment"), options), options);
                parent.Classes.Add("cd-vert-arrow");
                return parent;
            },
            MathmlBuilder = (group, options) =>
            {
                return new MathNode("mrow",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("fragment"), options) });
            },
        });
    }
}

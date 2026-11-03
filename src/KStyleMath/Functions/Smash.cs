namespace KStyleMath;

// smash, with optional [tb], as in AMS
public static class SmashFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "smash",
            Names = new List<string> { "\\smash" },
            NumArgs = 1,
            NumOptionalArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                bool smashHeight = false;
                bool smashDepth = false;
                var tbArg = optArgs[0] != null
                    ? ParseNodeUtil.AssertNodeType(optArgs[0], "ordgroup") : null;
                if (tbArg != null)
                {
                    // Optional [tb] argument is engaged.
                    // ref: amsmath: \renewcommand{\smash}[1][tb]{%
                    //               def\mb@t{\ht}\def\mb@b{\dp}\def\mb@tb{\ht\z@\z@\dp}%
                    for (int i = 0; i < tbArg.List("body").Count; ++i)
                    {
                        var node = tbArg.List("body")[i];
                        var letter = ParseNodeUtil.AssertSymbolNodeType(node).Str("text");
                        if (letter == "t")
                        {
                            smashHeight = true;
                        }
                        else if (letter == "b")
                        {
                            smashDepth = true;
                        }
                        else
                        {
                            smashHeight = false;
                            smashDepth = false;
                            break;
                        }
                    }
                }
                else
                {
                    smashHeight = true;
                    smashDepth = true;
                }

                var body = args[0];
                return new ParseNode("smash", context.Parser.Mode)
                {
                    ["body"] = body,
                    ["smashHeight"] = smashHeight,
                    ["smashDepth"] = smashDepth,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var node = BuildCommon.MakeSpan(
                    new List<string>(),
                    new List<VirtualNode> { BuildHTML.BuildGroup(group.Node("body"), options) });

                if (!group.Bool("smashHeight") && !group.Bool("smashDepth"))
                {
                    return node;
                }

                if (group.Bool("smashHeight"))
                {
                    node.Height = 0;
                }

                if (group.Bool("smashDepth"))
                {
                    node.Depth = 0;
                }

                if (group.Bool("smashHeight") && group.Bool("smashDepth"))
                {
                    // Symmetric \smash can stay in inline layout.
                    return BuildCommon.MakeSpan(
                        new List<string> { "mord", "katex-smash" },
                        new List<VirtualNode> { node }, options);
                }

                // In order to influence makeVList for asymmetric smashing, we have to
                // reset the children.
                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        if (group.Bool("smashHeight") && node.Children[i] is HtmlDomNode heightChild)
                        {
                            heightChild.Height = 0;
                        }
                        if (group.Bool("smashDepth") && node.Children[i] is HtmlDomNode depthChild)
                        {
                            depthChild.Depth = 0;
                        }
                    }
                }

                // At this point, we've reset the TeX-like height and depth values.
                // But the span still has an HTML line height.
                // makeVList applies "display: table-cell", which prevents the browser
                // from acting on that line height. So we'll call makeVList now.

                var smashedNode = BuildCommon.MakeVList(
                    VListParam.FirstBaseline(new List<object>
                    {
                        new VListElem { Elem = node },
                    }), options);

                // For spacing, TeX treats \smash as a math group (same spacing as ord).
                return BuildCommon.MakeSpan(
                    new List<string> { "mord" },
                    new List<VirtualNode> { smashedNode }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                var node = new MathNode(
                    "mpadded",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("body"), options) });

                if (group.Bool("smashHeight"))
                {
                    node.SetAttribute("height", "0px");
                }

                if (group.Bool("smashDepth"))
                {
                    node.SetAttribute("depth", "0px");
                }

                return node;
            },
        });
    }
}

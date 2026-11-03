namespace KStyleMath;

// Horizontal overlap functions

public static class LapFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "lap",
            Names = new List<string> { "\\mathllap", "\\mathrlap", "\\mathclap" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var body = args[0];
                return new ParseNode("lap", parser.Mode)
                {
                    ["alignment"] = funcName.Substring(5),
                    ["body"] = body,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // mathllap, mathrlap, mathclap
                Span inner;
                if (group.Str("alignment") == "clap")
                {
                    // ref: https://www.math.lsu.edu/~aperlis/publications/mathclap/
                    inner = BuildCommon.MakeSpan(
                        new List<string>(),
                        new List<VirtualNode> { BuildHTML.BuildGroup(group.Node("body"), options) });
                    // wrap, since CSS will center a .clap > .katex-inner > span
                    inner = BuildCommon.MakeSpan(
                        new List<string> { "katex-inner" },
                        new List<VirtualNode> { inner }, options);
                }
                else
                {
                    inner = BuildCommon.MakeSpan(
                        new List<string> { "katex-inner" },
                        new List<VirtualNode> { BuildHTML.BuildGroup(group.Node("body"), options) });
                }
                var fix = BuildCommon.MakeSpan(new List<string> { "katex-fix" },
                    new List<VirtualNode>());
                var node = BuildCommon.MakeSpan(
                    new List<string> { group.Str("alignment") },
                    new List<VirtualNode> { inner, fix }, options);

                // At this point, we have correctly set horizontal alignment of the
                // two items involved in the lap.
                // Next, use a strut to set the height of the HTML bounding box.
                // Otherwise, a tall argument may be misplaced.
                // This code resolved issue #1153
                var strut = BuildCommon.MakeSpan(new List<string> { "katex-strut" });
                strut.Style.Height = Units.MakeEm(node.Height + node.Depth);
                if (node.Depth != 0)
                {
                    strut.Style.VerticalAlign = Units.MakeEm(-node.Depth);
                }
                node.Children.Insert(0, strut);

                // Next, prevent vertical misplacement when next to something tall.
                // This code resolves issue #1234
                node = BuildCommon.MakeSpan(new List<string> { "katex-thinbox" },
                    new List<VirtualNode> { node }, options);
                return BuildCommon.MakeSpan(new List<string> { "mord", "katex-vbox" },
                    new List<VirtualNode> { node }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                // mathllap, mathrlap, mathclap
                var node = new MathNode(
                    "mpadded", new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("body"), options) });

                if (group.Str("alignment") != "rlap")
                {
                    var offset = (group.Str("alignment") == "llap" ? "-1" : "-0.5");
                    node.SetAttribute("lspace", offset + "width");
                }
                node.SetAttribute("width", "0px");

                return node;
            },
        });
    }
}

namespace KStyleMath;

public static class UnderlineFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "underline",
            Names = new List<string> { "\\underline" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("underline", context.Parser.Mode)
                {
                    ["body"] = args[0],
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Underlines are handled in the TeXbook pg 443, Rule 10.
                // Build the inner group.
                var innerGroup = BuildHTML.BuildGroup(group.Node("body"), options);

                // Create the line to go below the body
                var line = BuildCommon.MakeLineSpan("underline-line", options);

                // Generate the vlist, with the appropriate kerns
                double defaultRuleThickness = options.FontMetrics().DefaultRuleThickness;
                var vlist = BuildCommon.MakeVList(
                    VListParam.Top(innerGroup.Height, new List<object>
                    {
                        new VListKern { Size = defaultRuleThickness },
                        new VListElem { Elem = line },
                        new VListKern { Size = 3 * defaultRuleThickness },
                        new VListElem { Elem = innerGroup },
                    }), options);

                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "katex-underline" },
                    new List<VirtualNode> { vlist }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                var op = new MathNode(
                    "mo", new List<VirtualNode> { new MathTextNode("\u203e") });
                op.SetAttribute("stretchy", "true");

                var node = new MathNode(
                    "munder",
                    new List<VirtualNode>
                    {
                        BuildMathML.BuildGroup(group.Node("body"), options), op,
                    });
                node.SetAttribute("accentunder", "true");

                return node;
            },
        });
    }
}

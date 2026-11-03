namespace KStyleMath;

public static class OverlineFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "overline",
            Names = new List<string> { "\\overline" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("overline", context.Parser.Mode)
                {
                    ["body"] = body,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Overlines are handled in the TeXbook pg 443, Rule 9.

                // Build the inner group in the cramped style.
                var innerGroup = BuildHTML.BuildGroup(group.Node("body"),
                    options.HavingCrampedStyle());

                // Create the line above the body
                var line = BuildCommon.MakeLineSpan("overline-line", options);

                // Generate the vlist, with the appropriate kerns
                double defaultRuleThickness = options.FontMetrics().DefaultRuleThickness;
                var vlist = BuildCommon.MakeVList(
                    VListParam.FirstBaseline(new List<object>
                    {
                        new VListElem { Elem = innerGroup },
                        new VListKern { Size = 3 * defaultRuleThickness },
                        new VListElem { Elem = line },
                        new VListKern { Size = defaultRuleThickness },
                    }), options);

                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "katex-overline" },
                    new List<VirtualNode> { vlist }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                var op = new MathNode(
                    "mo", new List<VirtualNode> { new MathTextNode("\u203e") });
                op.SetAttribute("stretchy", "true");

                var node = new MathNode(
                    "mover",
                    new List<VirtualNode>
                    {
                        BuildMathML.BuildGroup(group.Node("body"), options), op,
                    });
                node.SetAttribute("accent", "true");

                return node;
            },
        });
    }
}

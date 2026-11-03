namespace KStyleMath;

public static class AccentUnderFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "accentUnder",
            Names = new List<string>
            {
                "\\underleftarrow", "\\underrightarrow", "\\underleftrightarrow",
                "\\undergroup", "\\underlinesegment", "\\utilde",
            },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var baseNode = args[0];
                return new ParseNode("accentUnder", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["base"] = baseNode,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Treat under accents much like underlines.
                var innerGroup = BuildHTML.BuildGroup(group.Node("base"), options);

                var accentBody = Stretchy.StretchySvg(group, options);
                var kern = group.Str("label") == "\\utilde" ? 0.12 : 0;

                // Generate the vlist, with the appropriate kerns
                var vlist = BuildCommon.MakeVList(VListParam.Top(innerGroup.Height,
                    new List<object>
                    {
                        new VListElem
                        {
                            Elem = accentBody,
                            WrapperClasses = new List<string> { "svg-align" },
                        },
                        new VListKern { Size = kern },
                        new VListElem { Elem = innerGroup },
                    }), options);

                return BuildCommon.MakeSpan(new List<string> { "mord", "accentunder" },
                    new List<VirtualNode> { vlist }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                var accentNode = Stretchy.StretchyMathML(group.Str("label"));
                var node = new MathNode(
                    "munder",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("base"), options), accentNode }
                );
                node.SetAttribute("accentunder", "true");
                return node;
            },
        });
    }
}

namespace KStyleMath;

// Box manipulation
public static class RaiseboxFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "raisebox",
            Names = new List<string> { "\\raisebox" },
            NumArgs = 2,
            ArgTypes = new List<string> { "size", "hbox" },
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var amount = ParseNodeUtil.AssertNodeType(args[0], "size").Get<Measurement>("value");
                var body = args[1];
                return new ParseNode("raisebox", context.Parser.Mode)
                {
                    ["dy"] = amount,
                    ["body"] = body,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var body = BuildHTML.BuildGroup(group.Node("body"), options);
                double dy = Units.CalculateSize(group.Get<Measurement>("dy"), options);
                return BuildCommon.MakeVList(
                    VListParam.Shift(-dy, new List<object>
                    {
                        new VListElem { Elem = body },
                    }), options);
            },
            MathmlBuilder = (group, options) =>
            {
                var node = new MathNode(
                    "mpadded",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("body"), options) });
                var dy = group.Get<Measurement>("dy");
                node.SetAttribute("voffset", JsNumber.ToString(dy.Number) + dy.Unit);
                return node;
            },
        });
    }
}

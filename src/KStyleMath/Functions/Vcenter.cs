namespace KStyleMath;

// \vcenter:  Vertically center the argument group on the math axis.

public static class VcenterFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "vcenter",
            Names = new List<string> { "\\vcenter" },
            NumArgs = 1,
            ArgTypes = new List<string> { "original" }, // In LaTeX, \vcenter can act only on a box.
            AllowedInText = false,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("vcenter", context.Parser.Mode)
                {
                    ["body"] = args[0],
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var body = BuildHTML.BuildGroup(group.Node("body"), options);
                double axisHeight = options.FontMetrics().AxisHeight;
                double dy = 0.5 * ((body.Height - axisHeight) - (body.Depth + axisHeight));
                return BuildCommon.MakeVList(
                    VListParam.Shift(dy, new List<object>
                    {
                        new VListElem { Elem = body },
                    }), options);
            },
            MathmlBuilder = (group, options) =>
            {
                // There is no way to do this in MathML.
                // Write a class as a breadcrumb in case some post-processor wants
                // to perform a vcenter adjustment.
                // Wrap in mrow to ensure valid MathML when placed inside mo (e.g., \mathrel)
                var mpadded = new MathNode(
                    "mpadded",
                    new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("body"), options) },
                    new List<string> { "vcenter" });
                return new MathNode("mrow", new List<VirtualNode> { mpadded });
            },
        });
    }
}

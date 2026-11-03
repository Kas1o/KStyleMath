namespace KStyleMath;

// \hbox is provided for compatibility with LaTeX \vcenter.
// In LaTeX, \vcenter can act only on a box, as in
// \vcenter{\hbox{$\frac{a+b}{\dfrac{c}{d}}$}}
// This function by itself doesn't do anything but prevent a soft line break.

public static class HboxFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "hbox",
            Names = new List<string> { "\\hbox" },
            NumArgs = 1,
            ArgTypes = new List<string> { "text" },
            AllowedInText = true,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                return new ParseNode("hbox", parser.Mode)
                {
                    ["body"] = Registries.Ordargument(args[0]),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(group.List("body"), options.WithFont(""), false);
                return BuildCommon.MakeFragment(elements.Cast<VirtualNode>().ToList());
            },
            MathmlBuilder = (group, options) =>
            {
                return new MathNode(
                    "mrow", new List<VirtualNode>(
                        BuildMathML.BuildExpression(group.List("body"), options.WithFont(""))));
            },
        });
    }
}

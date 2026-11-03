namespace KStyleMath;

public static class HtmlMathmlFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "htmlmathml",
            Names = new List<string> { "\\html@mathml" },
            NumArgs = 2,
            AllowedInArgument = true,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                return new ParseNode("htmlmathml", parser.Mode)
                {
                    ["html"] = Registries.Ordargument(args[0]),
                    ["mathml"] = Registries.Ordargument(args[1]),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(
                    group.List("html"),
                    options,
                    false
                );
                return BuildCommon.MakeFragment(elements.Cast<VirtualNode>().ToList());
            },
            MathmlBuilder = (group, options) =>
            {
                return BuildMathML.BuildExpressionRow(group.List("mathml"), options);
            },
        });
    }
}

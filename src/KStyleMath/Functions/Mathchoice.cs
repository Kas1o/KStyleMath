namespace KStyleMath;

public static class MathchoiceFunctions
{
    private static List<ParseNode> ChooseMathStyle(ParseNode group, Options options)
    {
        if (options.Style.Size == Style.DISPLAY.Size) return group.List("display");
        if (options.Style.Size == Style.TEXT.Size) return group.List("text");
        if (options.Style.Size == Style.SCRIPT.Size) return group.List("script");
        if (options.Style.Size == Style.SCRIPTSCRIPT.Size) return group.List("scriptscript");
        return group.List("text");
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "mathchoice",
            Names = new List<string> { "\\mathchoice" },
            NumArgs = 4,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                return new ParseNode("mathchoice", parser.Mode)
                {
                    ["display"] = Registries.Ordargument(args[0]),
                    ["text"] = Registries.Ordargument(args[1]),
                    ["script"] = Registries.Ordargument(args[2]),
                    ["scriptscript"] = Registries.Ordargument(args[3]),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var body = ChooseMathStyle(group, options);
                var elements = BuildHTML.BuildExpression(
                    body,
                    options,
                    false
                );
                return BuildCommon.MakeFragment(elements.Cast<VirtualNode>().ToList());
            },
            MathmlBuilder = (group, options) =>
            {
                var body = ChooseMathStyle(group, options);
                return BuildMathML.BuildExpressionRow(body, options);
            },
        });
    }
}

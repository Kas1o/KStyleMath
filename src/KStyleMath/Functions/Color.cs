namespace KStyleMath;

public static class ColorFunctions
{
    public static void Define()
    {
        Func<ParseNode, Options, VirtualNode> htmlBuilder = (group, options) =>
        {
            var elements = BuildHTML.BuildExpression(
                group.List("body"),
                options.WithColor(group.Str("color")),
                false);

            // \color isn't supposed to affect the type of the elements it contains.
            // To accomplish this, we wrap the results in a fragment, so the inner
            // elements will be able to directly interact with their neighbors. For
            // example, `\color{red}{2 +} 3` has the same spacing as `2 + 3`
            return BuildCommon.MakeFragment(elements.Cast<VirtualNode>().ToList());
        };

        Func<ParseNode, Options, VirtualNode> mathmlBuilder = (group, options) =>
        {
            var inner = BuildMathML.BuildExpression(group.List("body"),
                options.WithColor(group.Str("color")));

            var node = new MathNode("mstyle", new List<VirtualNode>(inner));

            node.SetAttribute("mathcolor", group.Str("color"));

            return node;
        };

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "color",
            Names = new List<string> { "\\textcolor" },
            NumArgs = 2,
            AllowedInText = true,
            ArgTypes = new List<string> { "color", "original" },

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var color = ParseNodeUtil.AssertNodeType(args[0], "color-token").Str("color");
                var body = args[1];
                return new ParseNode("color", parser.Mode)
                {
                    ["color"] = color,
                    ["body"] = Registries.Ordargument(body),
                };
            },

            HtmlBuilder = htmlBuilder,
            MathmlBuilder = mathmlBuilder,
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "color",
            Names = new List<string> { "\\color" },
            NumArgs = 1,
            AllowedInText = true,
            ArgTypes = new List<string> { "color" },

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var breakOnTokenText = context.BreakOnTokenText;
                var color = ParseNodeUtil.AssertNodeType(args[0], "color-token").Str("color");

                // Set macro \current@color in current namespace to store the current
                // color, mimicking the behavior of color.sty.
                // This is currently used just to correctly color a \right
                // that follows a \color command.
                parser.Gullet.Macros.Set("\\current@color", color);

                // Parse out the implicit body that should be colored.
                var body = parser.ParseExpression(true, breakOnTokenText);

                return new ParseNode("color", parser.Mode)
                {
                    ["color"] = color,
                    ["body"] = body,
                };
            },
        });
    }
}

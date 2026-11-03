namespace KStyleMath;

// Row breaks within tabular environments, and line breaks at top level

public static class CrFunctions
{
    public static void Define()
    {
        // \DeclareRobustCommand\\{...\@xnewline}
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "cr",
            Names = new List<string> { "\\\\" },
            NumArgs = 0,
            NumOptionalArgs = 0,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var size = parser.Gullet.Future().Text == "["
                    ? parser.ParseSizeGroup(true) : null;
                var newLine = !parser.Settings.DisplayMode ||
                    !StrictHelper.HandleBool(parser.Settings.Strict,
                        "newLineInDisplayMode",
                        "In LaTeX, \\\\ or \\newline does nothing in display mode",
                        null, false);
                return new ParseNode("cr", parser.Mode)
                {
                    ["newLine"] = newLine,
                    ["size"] = size != null
                        ? ParseNodeUtil.AssertNodeType(size, "size").Get<Measurement>("value")
                        : null,
                };
            },

            // The following builders are called only at the top level,
            // not within tabular/array environments.

            HtmlBuilder = (group, options) =>
            {
                var span = BuildCommon.MakeSpan(new List<string> { "mspace" },
                    new List<VirtualNode>(), options);
                if (group.Bool("newLine"))
                {
                    span.Classes.Add("katex-newline");
                    var size = group.Get<Measurement>("size");
                    if (size != null)
                    {
                        span.Style.MarginTop =
                            Units.MakeEm(Units.CalculateSize(size, options));
                    }
                }
                return span;
            },

            MathmlBuilder = (group, options) =>
            {
                var node = new MathNode("mspace");
                if (group.Bool("newLine"))
                {
                    node.SetAttribute("linebreak", "newline");
                    var size = group.Get<Measurement>("size");
                    if (size != null)
                    {
                        node.SetAttribute("height",
                            Units.MakeEm(Units.CalculateSize(size, options)));
                    }
                }
                return node;
            },
        });
    }
}

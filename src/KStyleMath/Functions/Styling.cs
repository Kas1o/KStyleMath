using System.Linq;

namespace KStyleMath;

public static class StylingFunctions
{
    private static readonly Dictionary<string, Style> StyleMap = new(StringComparer.Ordinal)
    {
        ["display"] = Style.DISPLAY,
        ["text"] = Style.TEXT,
        ["script"] = Style.SCRIPT,
        ["scriptscript"] = Style.SCRIPTSCRIPT,
    };

    private static bool IsStyleStr(string s) => StyleMap.ContainsKey(s);

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "styling",
            Names = new List<string>
            {
                "\\displaystyle", "\\textstyle", "\\scriptstyle",
                "\\scriptscriptstyle",
            },
            NumArgs = 0,
            AllowedInText = true,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                // parse out the implicit body
                var body = context.Parser.ParseExpression(true, context.BreakOnTokenText);

                // TODO: Refactor to avoid duplicating styleMap in multiple places (e.g.
                // here and in buildHTML and de-dupe the enumeration of all the styles).
                var style = context.FuncName.Substring(1, context.FuncName.Length - 6);
                if (!IsStyleStr(style))
                {
                    throw new InvalidOperationException($"Unknown style: {style}");
                }
                return new ParseNode("styling", context.Parser.Mode)
                {
                    // Figure out what style to use by pulling out the style from
                    // the function name
                    ["style"] = style,
                    ["body"] = body,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Style changes are handled in the TeXbook on pg. 442, Rule 3.
                var newStyle = StyleMap[group.Str("style")];
                var newOptions = options.HavingStyle(newStyle);
                if (group.Bool("resetFont"))
                {
                    newOptions = newOptions.WithFont("");
                }
                return SizingFunctions.SizingGroup(group.List("body"), newOptions, options);
            },
            MathmlBuilder = (group, options) =>
            {
                // Figure out what style we're changing to.
                var newStyle = StyleMap[group.Str("style")];
                var newOptions = options.HavingStyle(newStyle);
                if (group.Bool("resetFont"))
                {
                    newOptions = newOptions.WithFont("");
                }

                var inner = BuildMathML.BuildExpression(group.List("body"), newOptions);

                var node = new MathNode("mstyle", inner.Cast<VirtualNode>().ToList());

                var styleAttributes = new Dictionary<string, string[]>
                {
                    ["display"] = new[] { "0", "true" },
                    ["text"] = new[] { "0", "false" },
                    ["script"] = new[] { "1", "false" },
                    ["scriptscript"] = new[] { "2", "false" },
                };

                var attr = styleAttributes[group.Str("style")];

                node.SetAttribute("scriptlevel", attr[0]);
                node.SetAttribute("displaystyle", attr[1]);

                return node;
            },
        });
    }
}

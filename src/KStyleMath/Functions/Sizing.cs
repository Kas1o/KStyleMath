using System.Linq;

namespace KStyleMath;

public static class SizingFunctions
{
    public static DocumentFragment SizingGroup(List<ParseNode> value, Options options,
        Options baseOptions)
    {
        var inner = BuildHTML.BuildExpression(value, options, false);
        double multiplier = options.SizeMultiplier / baseOptions.SizeMultiplier;

        // Add size-resetting classes to the inner list and set maxFontSize
        // manually. Handle nested size changes.
        for (int i = 0; i < inner.Count; i++)
        {
            int pos = inner[i].Classes.IndexOf("katex-sizing");
            if (pos < 0)
            {
                inner[i].Classes.AddRange(options.SizingClasses(baseOptions));
            }
            else if (inner[i].Classes[pos + 1] == "reset-size" + options.Size)
            {
                // This is a nested size change: e.g., inner[i] is the "b" in
                // `\Huge a \small b`. Override the old size (the `reset-` class)
                // but not the new size.
                inner[i].Classes[pos + 1] = "reset-size" + baseOptions.Size;
            }

            inner[i].Height *= multiplier;
            inner[i].Depth *= multiplier;
        }

        return BuildCommon.MakeFragment(inner.Cast<VirtualNode>().ToList());
    }

    private static readonly List<string> SizeFuncs = new()
    {
        "\\tiny", "\\sixptsize", "\\scriptsize", "\\footnotesize", "\\small",
        "\\normalsize", "\\large", "\\Large", "\\LARGE", "\\huge", "\\Huge",
    };

    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        // Handle sizing operators like \Huge. Real TeX doesn't actually allow
        // these functions inside of math expressions, so we do some special
        // handling.
        var newOptions = options.HavingSize((int)group.Num("size"));
        return SizingGroup(group.List("body"), newOptions, options);
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "sizing",
            Names = SizeFuncs,
            NumArgs = 0,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var body = context.Parser.ParseExpression(false, context.BreakOnTokenText);

                return new ParseNode("sizing", context.Parser.Mode)
                {
                    // Figure out what size to use based on the list of functions above
                    ["size"] = (double)(SizeFuncs.IndexOf(context.FuncName) + 1),
                    ["body"] = body,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = (group, options) =>
            {
                var newOptions = options.HavingSize((int)group.Num("size"));
                var inner = BuildMathML.BuildExpression(group.List("body"), newOptions);

                var node = new MathNode("mstyle", inner.Cast<VirtualNode>().ToList());

                // TODO(emily): This doesn't produce the correct size for nested size
                // changes, because we don't keep state of what style we're currently
                // in, so we can't reset the size to normal before changing it.  Now
                // that we're passing an options parameter we should be able to fix
                // this.
                node.SetAttribute("mathsize", Units.MakeEm(newOptions.SizeMultiplier));

                return node;
            },
        });
    }
}

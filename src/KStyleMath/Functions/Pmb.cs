using System.Linq;

namespace KStyleMath;

// \pmb is a simulation of bold font.
// The version of \pmb in ambsy.sty works by typesetting three copies
// with small offsets. We use CSS text-shadow.
// It's a hack. Not as good as a real bold font. Better than nothing.
public static class PmbFunctions
{
    // Ported from mclass.ts's binrelClass.
    private static string BinrelClass(ParseNode arg)
    {
        // \binrel@ spacing varies with (bin|rel|ord) of the atom in the argument.
        // (by rendering separately and with {}s before and after, and measuring
        // the change in spacing).  We'll do roughly the same by detecting the
        // atom type directly.
        var atom = arg.Type == "ordgroup" && arg.List("body").Count > 0
            ? arg.List("body")[0] : arg;
        if (atom.Type == "atom" &&
            (atom.Str("family") == "bin" || atom.Str("family") == "rel"))
        {
            return "m" + atom.Str("family");
        }
        else
        {
            return "mord";
        }
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "pmb",
            Names = new List<string> { "\\pmb" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("pmb", context.Parser.Mode)
                {
                    ["mclass"] = BinrelClass(args[0]),
                    ["body"] = Registries.Ordargument(args[0]),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(group.List("body"), options, true);
                var node = BuildCommon.MakeSpan(
                    new List<string> { group.Str("mclass") },
                    elements.Cast<VirtualNode>().ToList(), options);
                node.Style.TextShadow = "0.02em 0.01em 0.04px";
                return node;
            },
            MathmlBuilder = (group, style) =>
            {
                var inner = BuildMathML.BuildExpression(group.List("body"), style);
                // Wrap with an <mstyle> element.
                var node = new MathNode("mstyle", inner.Cast<VirtualNode>().ToList());
                node.SetAttribute("style", "text-shadow: 0.02em 0.01em 0.04px");
                return node;
            },
        });
    }
}

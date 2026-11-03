using System.Linq;

namespace KStyleMath;

public static class PhantomFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "phantom",
            Names = new List<string> { "\\phantom" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("phantom", context.Parser.Mode)
                {
                    ["body"] = Registries.Ordargument(body),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(
                    group.List("body"),
                    options.WithPhantom(),
                    false
                );

                // \phantom isn't supposed to affect the elements it contains.
                // See "color" for more details.
                return BuildCommon.MakeFragment(elements.Cast<VirtualNode>().ToList());
            },
            MathmlBuilder = (group, options) =>
            {
                var inner = BuildMathML.BuildExpression(group.List("body"), options);
                return new MathNode("mphantom", inner.Cast<VirtualNode>().ToList());
            },
        });

        MacrosData.DefineMacro("\\hphantom", "\\smash{\\phantom{#1}}");

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "vphantom",
            Names = new List<string> { "\\vphantom" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("vphantom", context.Parser.Mode)
                {
                    ["body"] = body,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var inner = BuildCommon.MakeSpan(
                    new List<string> { "katex-inner" },
                    new List<VirtualNode>
                    {
                        BuildHTML.BuildGroup(group.Node("body"), options.WithPhantom()),
                    });
                var fix = BuildCommon.MakeSpan(
                    new List<string> { "katex-fix" }, new List<VirtualNode>());
                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "rlap" },
                    new List<VirtualNode> { inner, fix }, options);
            },
            MathmlBuilder = (group, options) =>
            {
                var inner = BuildMathML.BuildExpression(
                    Registries.Ordargument(group.Node("body")), options);
                var phantom = new MathNode("mphantom", inner.Cast<VirtualNode>().ToList());
                var node = new MathNode("mpadded",
                    new List<VirtualNode> { phantom });
                node.SetAttribute("width", "0px");
                return node;
            },
        });
    }
}

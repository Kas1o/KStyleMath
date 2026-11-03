namespace KStyleMath;

public static class HrefFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "href",
            Names = new List<string> { "\\href" },
            NumArgs = 2,
            ArgTypes = new List<string> { "url", "original" },
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var body = args[1];
                var href = ParseNodeUtil.AssertNodeType(args[0], "url").Str("url");

                if (!parser.Settings.IsTrusted(new Dictionary<string, object>
                {
                    ["command"] = "\\href",
                    ["url"] = href,
                }))
                {
                    return parser.FormatUnsupportedCmd("\\href");
                }

                return new ParseNode("href", parser.Mode)
                {
                    ["href"] = href,
                    ["body"] = Registries.Ordargument(body),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(group.List("body"), options, false);
                return BuildCommon.MakeAnchor(group.Str("href"), new List<string>(),
                    elements.Cast<VirtualNode>().ToList(), options);
            },
            MathmlBuilder = (group, options) =>
            {
                var math = BuildMathML.BuildExpressionRow(group.List("body"), options);
                if (math is not MathNode)
                {
                    math = new MathNode("mrow", new List<VirtualNode> { math });
                }
                ((MathNode)math).SetAttribute("href", group.Str("href"));
                return math;
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "href",
            Names = new List<string> { "\\url" },
            NumArgs = 1,
            ArgTypes = new List<string> { "url" },
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var href = ParseNodeUtil.AssertNodeType(args[0], "url").Str("url");

                if (!parser.Settings.IsTrusted(new Dictionary<string, object>
                {
                    ["command"] = "\\url",
                    ["url"] = href,
                }))
                {
                    return parser.FormatUnsupportedCmd("\\url");
                }

                var chars = new List<ParseNode>();
                for (int i = 0; i < href.Length; i++)
                {
                    var c = href[i].ToString();
                    if (c == "~")
                    {
                        c = "\\textasciitilde";
                    }
                    chars.Add(new ParseNode("textord", "text")
                    {
                        ["text"] = c,
                    });
                }
                var body = new ParseNode("text", parser.Mode)
                {
                    ["font"] = "\\texttt",
                    ["body"] = chars,
                };
                return new ParseNode("href", parser.Mode)
                {
                    ["href"] = href,
                    ["body"] = Registries.Ordargument(body),
                };
            },
        });
    }
}

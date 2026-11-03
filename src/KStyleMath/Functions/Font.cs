namespace KStyleMath;

// TODO(kevinb): implement \\sl and \\sc

public static class FontFunctions
{
    private static readonly Dictionary<string, string> FontAliases = new(StringComparer.Ordinal)
    {
        ["\\Bbb"] = "\\mathbb",
        ["\\bold"] = "\\mathbf",
        ["\\frak"] = "\\mathfrak",
    };

    private static string BinrelClass(ParseNode arg)
    {
        // \binrel@ spacing varies with (bin|rel|ord) of the atom in the argument.
        // (by rendering separately and with {}s before and after, and measuring
        // the change in spacing).  We'll do roughly the same by detecting the
        // atom type directly.
        var atom = arg.Type == "ordgroup" && arg.List("body").Count > 0
            ? arg.List("body")[0]
            : arg;
        if (atom.Type == "atom" && (atom.Str("family") == "bin" || atom.Str("family") == "rel"))
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
        Func<ParseNode, Options, VirtualNode> htmlBuilder = (group, options) =>
        {
            var font = group.Str("font");
            var newOptions = options.WithFont(font);
            return BuildHTML.BuildGroup(group.Node("body"), newOptions);
        };

        Func<ParseNode, Options, VirtualNode> mathmlBuilder = (group, options) =>
        {
            var font = group.Str("font");
            var newOptions = options.WithFont(font);
            return BuildMathML.BuildGroup(group.Node("body"), newOptions);
        };

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "font",
            Names = new List<string>
            {
                // styles, except \boldsymbol defined below
                "\\mathrm", "\\mathit", "\\mathbf", "\\mathnormal", "\\mathsfit",

                // families
                "\\mathbb", "\\mathcal", "\\mathfrak", "\\mathscr", "\\mathsf",
                "\\mathtt",

                // aliases, except \bm defined below
                "\\Bbb", "\\bold", "\\frak",
            },
            NumArgs = 1,
            AllowedInArgument = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var body = Registries.NormalizeArgument(args[0]);
                var func = FontAliases.TryGetValue(funcName, out var alias)
                    ? alias
                    : funcName;
                return new ParseNode("font", parser.Mode)
                {
                    ["font"] = func.Substring(1),
                    ["body"] = body,
                };
            },

            HtmlBuilder = htmlBuilder,
            MathmlBuilder = mathmlBuilder,
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "mclass",
            Names = new List<string> { "\\boldsymbol", "\\bm" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var body = args[0];
                // amsbsy.sty's \boldsymbol uses \binrel spacing to inherit the
                // argument's bin|rel|ord status
                return new ParseNode("mclass", parser.Mode)
                {
                    ["mclass"] = BinrelClass(body),
                    ["body"] = new List<ParseNode>
                    {
                        new ParseNode("font", parser.Mode)
                        {
                            ["font"] = "boldsymbol",
                            ["body"] = body,
                        },
                    },
                    ["isCharacterBox"] = Utils.IsCharacterBox(body),
                };
            },
        });

        // Old font changing functions
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "font",
            Names = new List<string> { "\\rm", "\\sf", "\\tt", "\\bf", "\\it", "\\cal" },
            NumArgs = 0,
            AllowedInText = true,
            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var breakOnTokenText = context.BreakOnTokenText;
                var mode = parser.Mode;
                var body = parser.ParseExpression(true, breakOnTokenText);

                return new ParseNode("font", mode)
                {
                    ["font"] = "math" + funcName.Substring(1),
                    ["body"] = new ParseNode("ordgroup", parser.Mode)
                    {
                        ["body"] = body,
                    },
                };
            },
        });
    }
}

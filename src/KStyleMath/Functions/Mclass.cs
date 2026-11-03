namespace KStyleMath;

public static class MclassFunctions
{
    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        var elements = BuildHTML.BuildExpression(group.List("body"), options, true);
        return BuildCommon.MakeSpan(new List<string> { group.Str("mclass") },
            elements.Cast<VirtualNode>().ToList(), options);
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        MathNode node;
        var inner = BuildMathML.BuildExpression(group.List("body"), options);

        if (group.Str("mclass") == "minner")
        {
            node = new MathNode("mpadded", inner.Cast<VirtualNode>().ToList());
        }
        else if (group.Str("mclass") == "mord")
        {
            if (group.Bool("isCharacterBox"))
            {
                node = (MathNode)inner[0];
                node.Type = "mi";
            }
            else
            {
                node = new MathNode("mi", inner.Cast<VirtualNode>().ToList());
            }
        }
        else
        {
            if (group.Bool("isCharacterBox"))
            {
                node = (MathNode)inner[0];
                node.Type = "mo";
            }
            else
            {
                node = new MathNode("mo", inner.Cast<VirtualNode>().ToList());
            }

            // Set spacing based on what is the most likely adjacent atom type.
            // See TeXbook p170.
            if (group.Str("mclass") == "mbin")
            {
                node.SetAttribute("lspace", "0.22em"); // medium space
                node.SetAttribute("rspace", "0.22em");
            }
            else if (group.Str("mclass") == "mpunct")
            {
                node.SetAttribute("lspace", "0em");
                node.SetAttribute("rspace", "0.17em"); // thinspace
            }
            else if (group.Str("mclass") == "mopen" || group.Str("mclass") == "mclose")
            {
                node.SetAttribute("lspace", "0em");
                node.SetAttribute("rspace", "0em");
            }
            // MathML <mo> default space is 5/18 em, so <mrel> needs no action.
            // Ref: https://developer.mozilla.org/en-US/docs/Web/MathML/Element/mo
        }
        return node;
    }

    public static string BinRelClass(ParseNode arg)
    {
        // \binrel@ spacing varies with (bin|rel|ord) of the atom in the argument.
        // (by rendering separately and with {}s before and after, and measuring
        // the change in spacing).  We'll do roughly the same by detecting the
        // atom type directly.
        var atom = (arg.Type == "ordgroup" && arg.List("body").Count > 0
            ? arg.List("body")[0]
            : arg);
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
        // Math class commands except \mathop
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "mclass",
            Names = new List<string>
            {
                "\\mathord", "\\mathbin", "\\mathrel", "\\mathopen",
                "\\mathclose", "\\mathpunct", "\\mathinner",
            },
            NumArgs = 1,
            Primitive = true,
            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("mclass", context.Parser.Mode)
                {
                    ["mclass"] = "m" + context.FuncName.Substring(5),
                    ["body"] = Registries.Ordargument(body),
                    ["isCharacterBox"] = Utils.IsCharacterBox(body),
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // \@binrel{x}{y} renders like y but as mbin/mrel/mord if x is mbin/mrel/mord.
        // This is equivalent to \binrel@{x}\binrel@@{y} in AMSTeX.
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "mclass",
            Names = new List<string> { "\\@binrel" },
            NumArgs = 2,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("mclass", context.Parser.Mode)
                {
                    ["mclass"] = BinRelClass(args[0]),
                    ["body"] = Registries.Ordargument(args[1]),
                    ["isCharacterBox"] = Utils.IsCharacterBox(args[1]),
                };
            },
        });

        // Build a relation or stacked op by placing one symbol on top of another
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "mclass",
            Names = new List<string> { "\\stackrel", "\\overset", "\\underset" },
            NumArgs = 2,

            Handler = (context, args, optArgs) =>
            {
                var baseArg = args[1];
                var shiftedArg = args[0];

                string mclass;
                if (context.FuncName != "\\stackrel")
                {
                    // LaTeX applies \binrel spacing to \overset and \underset.
                    mclass = BinRelClass(baseArg);
                }
                else
                {
                    mclass = "mrel"; // for \stackrel
                }

                var baseOp = new ParseNode("op", baseArg.Mode)
                {
                    ["limits"] = true,
                    ["alwaysHandleSupSub"] = true,
                    ["parentIsSupSub"] = false,
                    ["symbol"] = false,
                    ["suppressBaseShift"] = context.FuncName != "\\stackrel",
                    ["body"] = Registries.Ordargument(baseArg),
                };

                var supsub = context.FuncName == "\\underset"
                    ? new ParseNode("supsub", shiftedArg.Mode)
                    {
                        ["base"] = baseOp, ["sub"] = shiftedArg,
                    }
                    : new ParseNode("supsub", shiftedArg.Mode)
                    {
                        ["base"] = baseOp, ["sup"] = shiftedArg,
                    };

                return new ParseNode("mclass", context.Parser.Mode)
                {
                    ["mclass"] = mclass,
                    ["body"] = new List<ParseNode> { supsub },
                    ["isCharacterBox"] = Utils.IsCharacterBox(supsub),
                };
            },
        });
    }
}

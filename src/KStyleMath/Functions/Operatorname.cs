using System.Linq;

namespace KStyleMath;

// NOTE: Unlike most `htmlBuilder`s, this one handles not only
// "operatorname", but also "supsub" since \operatorname* can
// affect super/subscripting.
public static class OperatornameFunctions
{
    public static Span HtmlBuilder(ParseNode grp, Options options)
    {
        // Operators are handled in the TeXbook pg. 443-444, rule 13(a).
        ParseNode supGroup = null;
        ParseNode subGroup = null;
        bool hasLimits = false;
        ParseNode group;
        if (grp.Type == "supsub")
        {
            // If we have limits, supsub will pass us its group to handle. Pull
            // out the superscript and subscript and set the group to the op in
            // its base.
            supGroup = grp.Node("sup");
            subGroup = grp.Node("sub");
            group = ParseNodeUtil.AssertNodeType(grp.Node("base"), "operatorname");
            hasLimits = true;
        }
        else
        {
            group = ParseNodeUtil.AssertNodeType(grp, "operatorname");
        }

        Span baseNode;
        if (group.List("body").Count > 0)
        {
            var body = new List<ParseNode>();
            foreach (var child in group.List("body"))
            {
                var childText = child["text"] as string;
                if (childText != null)
                {
                    body.Add(new ParseNode("textord", child.Mode) { ["text"] = childText });
                }
                else
                {
                    body.Add(child);
                }
            }

            // Consolidate function names into symbol characters.
            var expression = BuildHTML.BuildExpression(
                body, options.WithFont("mathrm"), true);

            foreach (var child in expression)
            {
                if (child is SymbolNode symbolNode)
                {
                    // Per amsopn package,
                    // change minus to hyphen and \ast to asterisk
                    symbolNode.Text = symbolNode.Text.Replace("\u2212", "-")
                        .Replace("\u2217", "*");
                }
            }
            baseNode = BuildCommon.MakeSpan(new List<string> { "mop" },
                expression.Cast<VirtualNode>().ToList(), options);
        }
        else
        {
            baseNode = BuildCommon.MakeSpan(new List<string> { "mop" },
                new List<VirtualNode>(), options);
        }

        if (hasLimits)
        {
            return AssembleSupSub.AssembleSupSubFunc(baseNode, supGroup, subGroup, options,
                options.Style, 0, 0);
        }
        else
        {
            return baseNode;
        }
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        // The steps taken here are similar to the html version.
        var expression = BuildMathML.BuildExpression(
            group.List("body"), options.WithFont("mathrm"));

        // Is expression a string or has it something like a fraction?
        bool isAllString = true;  // default
        foreach (var node in expression)
        {
            if (node is SpaceNode)
            {
                // Do nothing
            }
            else if (node is MathNode mathNode && (mathNode.Type == "mi" || mathNode.Type == "mn" || mathNode.Type == "mspace" ||
                mathNode.Type == "mtext"))
            {
                // Do nothing yet.
            }
            else if (node is MathNode moNode && moNode.Type == "mo")
            {
                if (moNode.Children.Count == 1 && moNode.Children[0] is MathTextNode child)
                {
                    child.Text = child.Text.Replace("\u2212", "-")
                        .Replace("\u2217", "*");
                }
                else
                {
                    isAllString = false;
                }
            }
            else
            {
                isAllString = false;
            }
        }

        List<VirtualNode> expr = expression.Cast<VirtualNode>().ToList();
        if (isAllString)
        {
            // Write a single TextNode instead of multiple nested tags.
            string word = string.Concat(expression.Select(node => node is IMathDomNode md ? md.ToText() : ""));
            expr = new List<VirtualNode> { new MathTextNode(word) };
        }

        var identifier = new MathNode("mi", expr);
        identifier.SetAttribute("mathvariant", "normal");

        // \u2061 is the same as &ApplyFunction;
        // ref: https://www.w3schools.com/charsets/ref_html_entities_a.asp
        var op = new MathNode("mo",
            new List<VirtualNode> { BuildMathML.MakeText("\u2061", "text") });

        if (group.Bool("parentIsSupSub"))
        {
            return new MathNode("mrow", new List<VirtualNode> { identifier, op });
        }
        else
        {
            return MathMLTree.NewDocumentFragment(
                new List<VirtualNode> { identifier, op });
        }
    }

    // \operatorname
    // amsopn.dtx: \mathop{#1\kern\z@\operator@font#3}\newmcodes@
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "operatorname",
            Names = new List<string> { "\\operatorname@", "\\operatornamewithlimits" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("operatorname", context.Parser.Mode)
                {
                    ["body"] = Registries.Ordargument(body),
                    ["alwaysHandleSupSub"] = context.FuncName == "\\operatornamewithlimits",
                    ["limits"] = false,
                    ["parentIsSupSub"] = false,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        MacrosData.DefineMacro("\\operatorname",
            "\\@ifstar\\operatornamewithlimits\\operatorname@");
    }
}





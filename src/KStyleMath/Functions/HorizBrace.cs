namespace KStyleMath;

public static class HorizBraceFunctions
{
    // NOTE: Unlike most `htmlBuilder`s, this one handles not only "horizBrace", but
    // also "supsub" since an over/underbrace can affect super/subscripting.
    public static VirtualNode HtmlBuilder(ParseNode grp, Options options)
    {
        var style = options.Style;

        // Pull out the `ParseNode<"horizBrace">` if `grp` is a "supsub" node.
        HtmlDomNode supSubGroup = null;
        ParseNode group;
        if (grp.Type == "supsub")
        {
            // Ref: LaTeX source2e: }}}}\limits}
            // i.e. LaTeX treats the brace similar to an op and passes it
            // with \limits, so we need to assign supsub style.
            supSubGroup = grp.Node("sup") != null ?
                BuildHTML.BuildGroup(grp.Node("sup"), options.HavingStyle(style.Sup()), options) :
                BuildHTML.BuildGroup(grp.Node("sub"), options.HavingStyle(style.Sub()), options);
            group = ParseNodeUtil.AssertNodeType(grp.Node("base"), "horizBrace");
        }
        else
        {
            group = ParseNodeUtil.AssertNodeType(grp, "horizBrace");
        }

        // Build the base group
        var body = BuildHTML.BuildGroup(
            group.Node("base"), options.HavingBaseStyle(Style.DISPLAY));

        // Create the stretchy element
        var braceBody = Stretchy.StretchySvg(group, options);

        // Generate the vlist, with the appropriate kerns        ┏━━━━━━━━┓
        // This first vlist contains the content and the brace:   equation
        Span vlist;
        if (group.Bool("isOver"))
        {
            vlist = BuildCommon.MakeVList(VListParam.FirstBaseline(new List<object>
            {
                new VListElem { Elem = body },
                new VListKern { Size = 0.1 },
                new VListElem
                {
                    Elem = braceBody,
                    WrapperClasses = new List<string> { "svg-align" },
                },
            }), options);
        }
        else
        {
            vlist = BuildCommon.MakeVList(VListParam.Bottom(body.Depth + 0.1 + braceBody.Height,
                new List<object>
                {
                    new VListElem
                    {
                        Elem = braceBody,
                        WrapperClasses = new List<string> { "svg-align" },
                    },
                    new VListKern { Size = 0.1 },
                    new VListElem { Elem = body },
                }), options);
        }

        if (supSubGroup != null)
        {
            // To write the supsub, wrap the first vlist in another vlist:
            // They can't all go in the same vlist, because the note might be
            // wider than the equation. We want the equation to control the
            // brace width.

            //      note          long note           long note
            //   ┏━━━━━━━━┓   or    ┏━━━┓     not    ┏━━━━━━━━━┓
            //    equation           eqn                 eqn

            var vSpan = BuildCommon.MakeSpan(
                new List<string> { "minner", (group.Bool("isOver") ? "mover" : "munder") },
                new List<VirtualNode> { vlist }, options);

            if (group.Bool("isOver"))
            {
                vlist = BuildCommon.MakeVList(VListParam.FirstBaseline(new List<object>
                {
                    new VListElem { Elem = vSpan },
                    new VListKern { Size = 0.2 },
                    new VListElem { Elem = supSubGroup },
                }), options);
            }
            else
            {
                vlist = BuildCommon.MakeVList(VListParam.Bottom(
                    vSpan.Depth + 0.2 + supSubGroup.Height + supSubGroup.Depth,
                    new List<object>
                    {
                        new VListElem { Elem = supSubGroup },
                        new VListKern { Size = 0.2 },
                        new VListElem { Elem = vSpan },
                    }), options);
            }
        }

        return BuildCommon.MakeSpan(
            new List<string> { "minner", (group.Bool("isOver") ? "mover" : "munder") },
            new List<VirtualNode> { vlist }, options);
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var accentNode = Stretchy.StretchyMathML(group.Str("label"));
        return new MathNode(
            (group.Bool("isOver") ? "mover" : "munder"),
            new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("base"), options), accentNode }
        );
    }

    public static void Define()
    {
        // Horizontal stretchy braces
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "horizBrace",
            Names = new List<string> { "\\overbrace", "\\underbrace", "\\overbracket", "\\underbracket" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("horizBrace", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["isOver"] = context.FuncName.Contains("\\over"),
                    ["base"] = args[0],
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });
    }
}

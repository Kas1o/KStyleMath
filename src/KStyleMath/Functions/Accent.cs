using System.Text.RegularExpressions;

namespace KStyleMath;

public static class AccentFunctions
{
    private static SymbolNode GetBaseSymbol(HtmlDomNode group)
    {
        if (group is SymbolNode symbol)
        {
            return symbol;
        }
        List<VirtualNode> children = group switch
        {
            Span span => span.Children,
            Anchor anchor => anchor.Children,
            DocumentFragment fragment => fragment.Children,
            _ => null,
        };
        if (children != null && children.Count == 1 && children[0] is HtmlDomNode child)
        {
            return GetBaseSymbol(child);
        }
        return null;
    }

    // NOTE: Unlike most `htmlBuilder`s, this one handles not only "accent", but
    // also "supsub" since an accent can affect super/subscripting.
    public static Span HtmlBuilder(ParseNode grp, Options options)
    {
        // Accents are handled in the TeXbook pg. 443, rule 12.
        ParseNode baseNode;
        ParseNode group;

        Span supSubGroup = null;
        if (grp != null && grp.Type == "supsub")
        {
            // If our base is a character box, and we have superscripts and
            // subscripts, the supsub will defer to us. In particular, we want
            // to attach the superscripts and subscripts to the inner body (so
            // that the position of the superscripts and subscripts won't be
            // affected by the height of the accent). We accomplish this by
            // sticking the base of the accent into the base of the supsub, and
            // rendering that, while keeping track of where the accent is.

            // The real accent group is the base of the supsub group
            group = ParseNodeUtil.AssertNodeType(grp.Node("base"), "accent");
            // The character box is the base of the accent group
            baseNode = group.Node("base");
            // Stick the character box into the base of the supsub group
            grp["base"] = baseNode;

            // Rerender the supsub group with its new base, and store that
            // result.
            supSubGroup = (Span)BuildHTML.BuildGroup(grp, options);

            // reset original base
            grp["base"] = group;
        }
        else
        {
            group = ParseNodeUtil.AssertNodeType(grp, "accent");
            baseNode = group.Node("base");
        }

        // Build the base group
        var body = BuildHTML.BuildGroup(baseNode, options.HavingCrampedStyle());

        // Does the accent need to shift for the skew of a character?
        var mustShift = group.Bool("isShifty") && Utils.IsCharacterBox(baseNode);

        // Calculate the skew of the accent. This is based on the line "If the
        // nucleus is not a single character, let s = 0; otherwise set s to the
        // kern amount for the nucleus followed by the \skewchar of its font."
        // Note that our skew metrics are just the kern between each character
        // and the skewchar.
        double skew = 0;
        if (mustShift)
        {
            // Read the skew from the rendered base symbol.
            // This preserves font metrics from font wrappers like \mathbb.
            skew = GetBaseSymbol(body)?.Skew ?? 0;
        }

        var accentBelow = group.Str("label") == "\\c";

        // calculate the amount of space between the body and the accent
        var clearance = accentBelow
            ? body.Height + body.Depth
            : Math.Min(
                body.Height,
                options.FontMetrics().XHeight);

        // Build the accent
        Span accentBody;
        if (!group.Bool("isStretchy"))
        {
            HtmlDomNode accent;
            double width;
            if (group.Str("label") == "\\vec")
            {
                // Before version 0.9, \vec used the combining font glyph U+20D7.
                // But browsers, especially Safari, are not consistent in how they
                // render combining characters when not preceded by a character.
                // So now we use an SVG.
                // If Safari reforms, we should consider reverting to the glyph.
                accent = BuildCommon.StaticSvg("vec", options);
                width = BuildCommon.SvgData["vec"].Width;
            }
            else
            {
                accent = BuildCommon.MakeOrd(new ParseNode("textord", group.Mode)
                {
                    ["text"] = group.Str("label"),
                }, options);
                var accentSymbol = (SymbolNode)accent;
                // Remove the italic correction of the accent, because it only serves to
                // shift the accent over to a place we don't want.
                accentSymbol.Italic = 0;
                accent = accentSymbol;
                width = accentSymbol.Width;
                if (accentBelow)
                {
                    clearance += accentSymbol.Depth;
                }
            }

            accentBody = BuildCommon.MakeSpan(new List<string> { "accent-body" },
                new List<VirtualNode> { accent });

            // "Full" accents expand the width of the resulting symbol to be
            // at least the width of the accent, and overlap directly onto the
            // character without any vertical offset.
            var accentFull = group.Str("label") == "\\textcircled";
            if (accentFull)
            {
                accentBody.Classes.Add("accent-full");
                clearance = body.Height;
            }

            // Shift the accent over by the skew.
            double left = skew;

            // CSS defines `.katex .katex-accent .accent-body:not(.accent-full) { width: 0 }`
            // so that the accent doesn't contribute to the bounding box.
            // We need to shift the character by its width (effectively half
            // its width) to compensate.
            if (!accentFull)
            {
                left -= width / 2;
            }

            accentBody.Style.Left = Units.MakeEm(left);

            // \textcircled uses the \bigcirc glyph, so it needs some
            // vertical adjustment to match LaTeX.
            if (group.Str("label") == "\\textcircled")
            {
                accentBody.Style.Top = ".2em";
            }

            accentBody = BuildCommon.MakeVList(VListParam.FirstBaseline(new List<object>
            {
                new VListElem { Elem = body },
                new VListKern { Size = -clearance },
                new VListElem { Elem = accentBody },
            }), options);
        }
        else
        {
            var stretchyAccent = Stretchy.StretchySvg(group, options);

            CssStyle wrapperStyle = null;
            if (skew > 0)
            {
                wrapperStyle = new CssStyle
                {
                    Width = "calc(100% - " + Units.MakeEm(2 * skew) + ")",
                    MarginLeft = Units.MakeEm(2 * skew),
                };
            }

            accentBody = BuildCommon.MakeVList(VListParam.FirstBaseline(new List<object>
            {
                new VListElem { Elem = body },
                new VListElem
                {
                    Elem = stretchyAccent,
                    WrapperClasses = new List<string> { "svg-align" },
                    WrapperStyle = wrapperStyle,
                },
            }), options);
        }

        var accentWrap =
            BuildCommon.MakeSpan(new List<string> { "mord", "katex-accent" },
                new List<VirtualNode> { accentBody }, options);

        if (supSubGroup != null)
        {
            // Here, we replace the "base" child of the supsub with our newly
            // generated accent.
            supSubGroup.Children[0] = accentWrap;

            // Since we don't rerun the height calculation after replacing the
            // accent, we manually recalculate height.
            supSubGroup.Height = Math.Max(accentWrap.Height, supSubGroup.Height);

            // Accents should always be ords, even when their innards are not.
            supSubGroup.Classes[0] = "mord";

            return supSubGroup;
        }
        else
        {
            return accentWrap;
        }
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var accentNode =
            group.Bool("isStretchy")
                ? (VirtualNode)Stretchy.StretchyMathML(group.Str("label"))
                : new MathNode("mo", new List<VirtualNode>
                    { BuildMathML.MakeText(group.Str("label"), group.Mode) });

        var node = new MathNode(
            "mover",
            new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("base"), options), accentNode });

        node.SetAttribute("accent", "true");

        return node;
    }

    private static readonly Regex NonStretchyAccentRegex = new(string.Join("|", new[]
    {
        @"\\acute", @"\\grave", @"\\ddot", @"\\tilde", @"\\bar", @"\\breve",
        @"\\check", @"\\hat", @"\\vec", @"\\dot", @"\\mathring",
    }));

    public static void Define()
    {
        // Accents
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "accent",
            Names = new List<string>
            {
                "\\acute", "\\grave", "\\ddot", "\\tilde", "\\bar", "\\breve",
                "\\check", "\\hat", "\\vec", "\\dot", "\\mathring", "\\widecheck",
                "\\widehat", "\\widetilde", "\\overrightarrow", "\\overleftarrow",
                "\\Overrightarrow", "\\overleftrightarrow", "\\overgroup",
                "\\overlinesegment", "\\overleftharpoon", "\\overrightharpoon",
            },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var baseNode = Registries.NormalizeArgument(args[0]);

                var isStretchy = !NonStretchyAccentRegex.IsMatch(context.FuncName);
                var isShifty = !isStretchy ||
                    context.FuncName == "\\widehat" ||
                    context.FuncName == "\\widetilde" ||
                    context.FuncName == "\\widecheck";

                return new ParseNode("accent", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["isStretchy"] = isStretchy,
                    ["isShifty"] = isShifty,
                    ["base"] = baseNode,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // Text-mode accents
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "accent",
            Names = new List<string>
            {
                "\\'", "\\`", "\\^", "\\~", "\\=", "\\u", "\\.", "\\\"",
                "\\c", "\\r", "\\H", "\\v", "\\textcircled",
            },
            NumArgs = 1,
            AllowedInText = true,
            AllowedInMath = true, // unless in strict mode
            ArgTypes = new List<string> { "primitive" },

            Handler = (context, args, optArgs) =>
            {
                var baseNode = args[0];
                var mode = context.Parser.Mode;

                if (mode == "math")
                {
                    StrictHelper.Handle(
                        context.Parser.Settings.Strict,
                        "mathVsTextAccents",
                        $"LaTeX's accent {context.FuncName} works only in text mode",
                        null,
                        true);
                    mode = "text";
                }

                return new ParseNode("accent", mode)
                {
                    ["label"] = context.FuncName,
                    ["isStretchy"] = false,
                    ["isShifty"] = true,
                    ["base"] = baseNode,
                };
            },
        });
    }
}

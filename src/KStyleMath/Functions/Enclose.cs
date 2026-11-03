using System.Text.RegularExpressions;

namespace KStyleMath;

public static class EncloseFunctions
{
    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        // \cancel, \bcancel, \xcancel, \sout, \fbox, \colorbox, \fcolorbox, \phase
        // Some groups can return document fragments.  Handle those by wrapping
        // them in a span.
        var inner = BuildCommon.WrapFragment(
            BuildHTML.BuildGroup(group.Node("body"), options), options);

        var label = group.Str("label").Substring(1);
        var scale = options.SizeMultiplier;
        Span img;
        double imgShift;

        // In the LaTeX cancel package, line geometry is slightly different
        // depending on whether the subject is wider than it is tall, or vice versa.
        // We don't know the width of a group, so as a proxy, we test if
        // the subject is a single character. This captures most of the
        // subjects that should get the "tall" treatment.
        var isSingleChar = Utils.IsCharacterBox(group.Node("body"));

        if (label == "sout")
        {
            img = BuildCommon.MakeSpan(new List<string> { "katex-stretchy", "katex-sout" });
            img.Height = options.FontMetrics().DefaultRuleThickness / scale;
            imgShift = -0.5 * options.FontMetrics().XHeight;
        }
        else if (label == "phase")
        {
            // Set a couple of dimensions from the steinmetz package.
            var lineWeight = Units.CalculateSize(new Measurement(0.6, "pt"), options);
            var clearance = Units.CalculateSize(new Measurement(0.35, "ex"), options);

            // Prevent size changes like \Huge from affecting line thickness
            var newOptions = options.HavingBaseSizing();
            scale = scale / newOptions.SizeMultiplier;

            var angleHeight = inner.Height + inner.Depth + lineWeight + clearance;
            // Reserve a left pad for the angle.
            inner.Style.PaddingLeft = Units.MakeEm(angleHeight / 2 + lineWeight);

            // Create an SVG
            var viewBoxHeight = Math.Floor(1000 * angleHeight * scale);
            var path = SvgGeometry.PhasePath(viewBoxHeight);
            var svgNode = new SvgNode(new List<VirtualNode> { new PathNode("phase", path) },
                new Dictionary<string, string>
                {
                    ["width"] = "400em",
                    ["height"] = Units.MakeEm(viewBoxHeight / 1000),
                    ["viewBox"] = $"0 0 400000 {JsNumber.ToString(viewBoxHeight)}",
                    ["preserveAspectRatio"] = "xMinYMin slice",
                });
            // Wrap it in a span with overflow: hidden.
            img = BuildCommon.MakeSvgSpan(new List<string> { "hide-tail" },
                new List<VirtualNode> { svgNode }, options);
            img.Style.Height = Units.MakeEm(angleHeight);
            imgShift = inner.Depth + lineWeight + clearance;
        }
        else
        {
            // Add horizontal padding
            if (Regex.IsMatch(label, "cancel"))
            {
                if (!isSingleChar)
                {
                    inner.Classes.Add("cancel-pad");
                }
            }
            else if (label == "angl")
            {
                inner.Classes.Add("anglpad");
            }
            else
            {
                inner.Classes.Add("boxpad");
            }

            // Add vertical padding
            double topPad;
            double bottomPad;
            double ruleThickness = 0;
            // ref: cancel package: \advance\totalheight2\p@ % "+2"
            if (Regex.IsMatch(label, "box"))
            {
                ruleThickness = Math.Max(
                    options.FontMetrics().Fboxrule, // default
                    options.MinRuleThickness // User override.
                );
                topPad = options.FontMetrics().Fboxsep +
                    (label == "colorbox" ? 0 : ruleThickness);
                bottomPad = topPad;
            }
            else if (label == "angl")
            {
                ruleThickness = Math.Max(
                    options.FontMetrics().DefaultRuleThickness,
                    options.MinRuleThickness
                );
                topPad = 4 * ruleThickness; // gap = 3 × line, plus the line itself.
                bottomPad = Math.Max(0, 0.25 - inner.Depth);
            }
            else
            {
                topPad = isSingleChar ? 0.2 : 0;
                bottomPad = topPad;
            }

            img = Stretchy.StretchyEnclose(inner, label, topPad, bottomPad, options);
            if (Regex.IsMatch(label, "fbox|boxed|fcolorbox"))
            {
                img.Style.BorderStyle = "solid";
                img.Style.BorderWidth = Units.MakeEm(ruleThickness);
            }
            else if (label == "angl" && ruleThickness != 0.049)
            {
                img.Style.BorderTopWidth = Units.MakeEm(ruleThickness);
                img.Style.BorderRightWidth = Units.MakeEm(ruleThickness);
            }
            imgShift = inner.Depth + bottomPad;

            if (group.Str("backgroundColor") != null)
            {
                img.Style.BackgroundColor = group.Str("backgroundColor");
                if (group.Str("borderColor") != null)
                {
                    img.Style.BorderColor = group.Str("borderColor");
                }
            }
        }

        Span vlist;
        if (group.Str("backgroundColor") != null)
        {
            vlist = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                // Put the color background behind inner;
                new() { Elem = img, Shift = imgShift },
                new() { Elem = inner, Shift = 0 },
            }), options);
        }
        else
        {
            var classes = Regex.IsMatch(label, "cancel|phase")
                ? new List<string> { "svg-align" }
                : new List<string>();
            vlist = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                // Write the \cancel stroke on top of inner.
                new()
                {
                    Elem = inner,
                    Shift = 0,
                },
                new()
                {
                    Elem = img,
                    Shift = imgShift,
                    WrapperClasses = classes,
                },
            }), options);
        }

        if (Regex.IsMatch(label, "cancel"))
        {
            // The cancel package documentation says that cancel lines add their height
            // to the expression, but tests show that isn't how it actually works.
            vlist.Height = inner.Height;
            vlist.Depth = inner.Depth;
        }

        if (Regex.IsMatch(label, "cancel") && !isSingleChar)
        {
            // cancel does not create horiz space for its line extension.
            return BuildCommon.MakeSpan(new List<string> { "mord", "cancel-lap" },
                new List<VirtualNode> { vlist }, options);
        }
        else
        {
            return BuildCommon.MakeSpan(new List<string> { "mord" },
                new List<VirtualNode> { vlist }, options);
        }
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        double fboxsep;
        var node = new MathNode(
            group.Str("label").Contains("colorbox") ? "mpadded" : "menclose",
            new List<VirtualNode> { BuildMathML.BuildGroup(group.Node("body"), options) }
        );
        switch (group.Str("label"))
        {
            case "\\cancel":
                node.SetAttribute("notation", "updiagonalstrike");
                break;
            case "\\bcancel":
                node.SetAttribute("notation", "downdiagonalstrike");
                break;
            case "\\phase":
                node.SetAttribute("notation", "phasorangle");
                break;
            case "\\sout":
                node.SetAttribute("notation", "horizontalstrike");
                break;
            case "\\fbox":
                node.SetAttribute("notation", "box");
                break;
            case "\\angl":
                node.SetAttribute("notation", "actuarial");
                break;
            case "\\fcolorbox":
            case "\\colorbox":
                // <menclose> doesn't have a good notation option. So use <mpadded>
                // instead. Set some attributes that come included with <menclose>.
                fboxsep = options.FontMetrics().Fboxsep *
                    options.FontMetrics().PtPerEm;
                node.SetAttribute("width", "+" + JsNumber.ToString(2 * fboxsep) + "pt");
                node.SetAttribute("height", "+" + JsNumber.ToString(2 * fboxsep) + "pt");
                node.SetAttribute("lspace", JsNumber.ToString(fboxsep) + "pt"); //
                node.SetAttribute("voffset", JsNumber.ToString(fboxsep) + "pt");
                if (group.Str("label") == "\\fcolorbox")
                {
                    var thk = Math.Max(
                        options.FontMetrics().Fboxrule, // default
                        options.MinRuleThickness // user override
                    );
                    node.SetAttribute("style", $"border: {Units.MakeEm(thk)} solid {group.Str("borderColor")}");
                }
                break;
            case "\\xcancel":
                node.SetAttribute("notation", "updiagonalstrike downdiagonalstrike");
                break;
        }
        if (group.Str("backgroundColor") != null)
        {
            node.SetAttribute("mathbackground", group.Str("backgroundColor"));
        }
        return node;
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\colorbox" },
            NumArgs = 2,
            AllowedInText = true,
            ArgTypes = new List<string> { "color", "hbox" },

            Handler = (context, args, optArgs) =>
            {
                var color = ParseNodeUtil.AssertNodeType(args[0], "color-token").Str("color");
                var body = args[1];
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["backgroundColor"] = color,
                    ["body"] = body,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\fcolorbox" },
            NumArgs = 3,
            AllowedInText = true,
            ArgTypes = new List<string> { "color", "color", "hbox" },

            Handler = (context, args, optArgs) =>
            {
                var borderColor = ParseNodeUtil.AssertNodeType(args[0], "color-token").Str("color");
                var backgroundColor = ParseNodeUtil.AssertNodeType(args[1], "color-token").Str("color");
                var body = args[2];
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["backgroundColor"] = backgroundColor,
                    ["borderColor"] = borderColor,
                    ["body"] = body,
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\fbox" },
            NumArgs = 1,
            ArgTypes = new List<string> { "hbox" },
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = "\\fbox",
                    ["body"] = args[0],
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\cancel", "\\bcancel", "\\xcancel", "\\phase" },
            NumArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["body"] = body,
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\sout" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                if (context.Parser.Mode == "math")
                {
                    StrictHelper.Handle(
                        context.Parser.Settings.Strict,
                        "mathVsSout",
                        "LaTeX's \\sout works only in text mode",
                        null,
                        true);
                }
                var body = args[0];
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = context.FuncName,
                    ["body"] = body,
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "enclose",
            Names = new List<string> { "\\angl" },
            NumArgs = 1,
            ArgTypes = new List<string> { "hbox" },
            AllowedInText = false,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("enclose", context.Parser.Mode)
                {
                    ["label"] = "\\angl",
                    ["body"] = args[0],
                };
            },
        });
    }
}

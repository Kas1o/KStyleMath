using System.Globalization;

namespace KStyleMath;

public static class GenfracFunctions
{
    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        // Fractions are handled in the TeXbook on pages 444-445, rules 15(a-e).
        var style = options.Style;

        var nstyle = style.FracNum();
        var dstyle = style.FracDen();
        Options newOptions;

        newOptions = options.HavingStyle(nstyle);
        var numerm = BuildHTML.BuildGroup(group.Node("numer"), newOptions, options);

        if (group.Bool("continued"))
        {
            // \cfrac inserts a \strut into the numerator.
            // Get \strut dimensions from TeXbook page 353.
            var hStrut = 8.5 / options.FontMetrics().PtPerEm;
            var dStrut = 3.5 / options.FontMetrics().PtPerEm;
            numerm.Height = numerm.Height < hStrut ? hStrut : numerm.Height;
            numerm.Depth = numerm.Depth < dStrut ? dStrut : numerm.Depth;
        }

        newOptions = options.HavingStyle(dstyle);
        var denomm = BuildHTML.BuildGroup(group.Node("denom"), newOptions, options);

        Span rule;
        double ruleWidth;
        double ruleSpacing;
        if (group.Bool("hasBarLine"))
        {
            var barSize = group.Get<Measurement>("barSize");
            if (barSize != null)
            {
                ruleWidth = Units.CalculateSize(barSize, options);
                rule = BuildCommon.MakeLineSpan("frac-line", options, ruleWidth);
            }
            else
            {
                rule = BuildCommon.MakeLineSpan("frac-line", options);
            }
            ruleWidth = rule.Height;
            ruleSpacing = rule.Height;
        }
        else
        {
            rule = null;
            ruleWidth = 0;
            ruleSpacing = options.FontMetrics().DefaultRuleThickness;
        }

        // Rule 15b
        double numShift;
        double clearance;
        double denomShift;
        if (style.Size == Style.DISPLAY.Size)
        {
            numShift = options.FontMetrics().Num1;
            if (ruleWidth > 0)
            {
                clearance = 3 * ruleSpacing;
            }
            else
            {
                clearance = 7 * ruleSpacing;
            }
            denomShift = options.FontMetrics().Denom1;
        }
        else
        {
            if (ruleWidth > 0)
            {
                numShift = options.FontMetrics().Num2;
                clearance = ruleSpacing;
            }
            else
            {
                numShift = options.FontMetrics().Num3;
                clearance = 3 * ruleSpacing;
            }
            denomShift = options.FontMetrics().Denom2;
        }

        Span frac;
        if (rule == null)
        {
            // Rule 15c
            var candidateClearance =
                (numShift - numerm.Depth) - (denomm.Height - denomShift);
            if (candidateClearance < clearance)
            {
                numShift += 0.5 * (clearance - candidateClearance);
                denomShift += 0.5 * (clearance - candidateClearance);
            }

            frac = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                new() { Elem = denomm, Shift = denomShift },
                new() { Elem = numerm, Shift = -numShift },
            }), options);
        }
        else
        {
            // Rule 15d
            var axisHeight = options.FontMetrics().AxisHeight;

            if ((numShift - numerm.Depth) - (axisHeight + 0.5 * ruleWidth) <
                clearance)
            {
                numShift +=
                    clearance - ((numShift - numerm.Depth) -
                                (axisHeight + 0.5 * ruleWidth));
            }

            if ((axisHeight - 0.5 * ruleWidth) - (denomm.Height - denomShift) <
                clearance)
            {
                denomShift +=
                    clearance - ((axisHeight - 0.5 * ruleWidth) -
                                (denomm.Height - denomShift));
            }

            var midShift = -(axisHeight - 0.5 * ruleWidth);

            frac = BuildCommon.MakeVList(VListParam.IndividualShift(new List<VListElem>
            {
                new() { Elem = denomm, Shift = denomShift },
                new() { Elem = rule, Shift = midShift },
                new() { Elem = numerm, Shift = -numShift },
            }), options);
        }

        // Since we manually change the style sometimes (with \dfrac or \tfrac),
        // account for the possible size change here.
        newOptions = options.HavingStyle(style);
        frac.Height *= newOptions.SizeMultiplier / options.SizeMultiplier;
        frac.Depth *= newOptions.SizeMultiplier / options.SizeMultiplier;

        // Rule 15e
        double delimSize;
        if (style.Size == Style.DISPLAY.Size)
        {
            delimSize = options.FontMetrics().Delim1;
        }
        else if (style.Size == Style.SCRIPTSCRIPT.Size)
        {
            delimSize = options.HavingStyle(Style.SCRIPT).FontMetrics().Delim2;
        }
        else
        {
            delimSize = options.FontMetrics().Delim2;
        }

        HtmlDomNode leftDelim;
        HtmlDomNode rightDelim;
        if (group.Str("leftDelim") == null)
        {
            leftDelim = BuildHTML.MakeNullDelimiter(options, new List<string> { "mopen" });
        }
        else
        {
            leftDelim = Delimiter.MakeCustomSizedDelim(
                group.Str("leftDelim"), delimSize, true,
                options.HavingStyle(style), group.Mode, new List<string> { "mopen" });
        }

        if (group.Bool("continued"))
        {
            rightDelim = BuildCommon.MakeSpan(new List<string>()); // zero width for \cfrac
        }
        else if (group.Str("rightDelim") == null)
        {
            rightDelim = BuildHTML.MakeNullDelimiter(options, new List<string> { "mclose" });
        }
        else
        {
            rightDelim = Delimiter.MakeCustomSizedDelim(
                group.Str("rightDelim"), delimSize, true,
                options.HavingStyle(style), group.Mode, new List<string> { "mclose" });
        }

        return BuildCommon.MakeSpan(
            new List<string> { "mord" }.Concat(newOptions.SizingClasses(options)).ToList(),
            new List<VirtualNode>
            {
                leftDelim,
                BuildCommon.MakeSpan(new List<string> { "mfrac" }, new List<VirtualNode> { frac }),
                rightDelim,
            },
            options);
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var node = new MathNode(
            "mfrac",
            new List<VirtualNode>
            {
                BuildMathML.BuildGroup(group.Node("numer"), options),
                BuildMathML.BuildGroup(group.Node("denom"), options),
            });

        var barSize = group.Get<Measurement>("barSize");
        if (!group.Bool("hasBarLine"))
        {
            node.SetAttribute("linethickness", "0px");
        }
        else if (barSize != null)
        {
            var ruleWidth = Units.CalculateSize(barSize, options);
            node.SetAttribute("linethickness", Units.MakeEm(ruleWidth));
        }

        if (group.Str("leftDelim") != null || group.Str("rightDelim") != null)
        {
            var withDelims = new List<VirtualNode>();

            if (group.Str("leftDelim") != null)
            {
                var leftOp = new MathNode(
                    "mo",
                    new List<VirtualNode> { new MathTextNode(group.Str("leftDelim").Replace("\\", "")) }
                );

                leftOp.SetAttribute("fence", "true");

                withDelims.Add(leftOp);
            }

            withDelims.Add(node);

            if (group.Str("rightDelim") != null)
            {
                var rightOp = new MathNode(
                    "mo",
                    new List<VirtualNode> { new MathTextNode(group.Str("rightDelim").Replace("\\", "")) }
                );

                rightOp.SetAttribute("fence", "true");

                withDelims.Add(rightOp);
            }

            return BuildMathML.MakeRow(withDelims);
        }

        return node;
    }

    private static ParseNode WrapWithStyle(ParseNode frac, string style)
    {
        if (string.IsNullOrEmpty(style))
        {
            return frac;
        }

        var wrapper = new ParseNode("styling", frac.Mode)
        {
            ["style"] = style,
            ["body"] = new List<ParseNode> { frac },
        };

        return wrapper;
    }

    private static readonly string[] StylArray = { "display", "text", "script", "scriptscript" };

    private static string DelimFromValue(string delimString)
    {
        string delim = null;
        if (delimString.Length > 0)
        {
            delim = delimString;
            delim = delim == "." ? null : delim;
        }
        return delim;
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "genfrac",
            Names = new List<string>
            {
                "\\cfrac", "\\dfrac", "\\frac", "\\tfrac",
                "\\dbinom", "\\binom", "\\tbinom",
                "\\\\atopfrac", // can’t be entered directly
                "\\\\bracefrac", "\\\\brackfrac", // ditto
            },
            NumArgs = 2,
            AllowedInArgument = true,

            Handler = (context, args, optArgs) =>
            {
                var numer = args[0];
                var denom = args[1];
                bool hasBarLine;
                string leftDelim = null;
                string rightDelim = null;

                switch (context.FuncName)
                {
                    case "\\cfrac":
                    case "\\dfrac":
                    case "\\frac":
                    case "\\tfrac":
                        hasBarLine = true;
                        break;
                    case "\\\\atopfrac":
                        hasBarLine = false;
                        break;
                    case "\\dbinom":
                    case "\\binom":
                    case "\\tbinom":
                        hasBarLine = false;
                        leftDelim = "(";
                        rightDelim = ")";
                        break;
                    case "\\\\bracefrac":
                        hasBarLine = false;
                        leftDelim = "\\{";
                        rightDelim = "\\}";
                        break;
                    case "\\\\brackfrac":
                        hasBarLine = false;
                        leftDelim = "[";
                        rightDelim = "]";
                        break;
                    default:
                        throw new InvalidOperationException("Unrecognized genfrac command");
                }

                var continued = context.FuncName == "\\cfrac";
                string style = null;
                if (continued || context.FuncName.StartsWith("\\d"))
                {
                    style = "display";
                }
                else if (context.FuncName.StartsWith("\\t"))
                {
                    style = "text";
                }

                return WrapWithStyle(new ParseNode("genfrac", context.Parser.Mode)
                {
                    ["numer"] = numer,
                    ["denom"] = denom,
                    ["continued"] = continued,
                    ["hasBarLine"] = hasBarLine,
                    ["leftDelim"] = leftDelim,
                    ["rightDelim"] = rightDelim,
                    ["barSize"] = null,
                }, style);
            },


            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // Infix generalized fractions -- these are not rendered directly, but replaced
        // immediately by one of the variants above.
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "infix",
            Names = new List<string> { "\\over", "\\choose", "\\atop", "\\brace", "\\brack" },
            NumArgs = 0,
            Infix = true,

            Handler = (context, args, optArgs) =>
            {
                string replaceWith;
                switch (context.FuncName)
                {
                    case "\\over":
                        replaceWith = "\\frac";
                        break;
                    case "\\choose":
                        replaceWith = "\\binom";
                        break;
                    case "\\atop":
                        replaceWith = "\\\\atopfrac";
                        break;
                    case "\\brace":
                        replaceWith = "\\\\bracefrac";
                        break;
                    case "\\brack":
                        replaceWith = "\\\\brackfrac";
                        break;
                    default:
                        throw new InvalidOperationException("Unrecognized infix genfrac command");
                }
                return new ParseNode("infix", context.Parser.Mode)
                {
                    ["replaceWith"] = replaceWith,
                    ["token"] = context.Token,
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "genfrac",
            Names = new List<string> { "\\genfrac" },
            NumArgs = 6,
            AllowedInArgument = true,
            ArgTypes = new List<string> { "math", "math", "size", "text", "math", "math" },

            Handler = (context, args, optArgs) =>
            {
                var numer = args[4];
                var denom = args[5];

                // Look into the parse nodes to get the desired delimiters.
                var leftNode = Registries.NormalizeArgument(args[0]);
                var leftDelim = leftNode.Type == "atom" && leftNode.Str("family") == "open"
                    ? DelimFromValue(leftNode.Str("text")) : null;
                var rightNode = Registries.NormalizeArgument(args[1]);
                var rightDelim = rightNode.Type == "atom" && rightNode.Str("family") == "close"
                    ? DelimFromValue(rightNode.Str("text")) : null;

                var barNode = ParseNodeUtil.AssertNodeType(args[2], "size");
                bool hasBarLine;
                Measurement barSize = null;
                if (barNode.Bool("isBlank"))
                {
                    // \genfrac acts differently than \above.
                    // \genfrac treats an empty size group as a signal to use a
                    // standard bar size. \above would see size = 0 and omit the bar.
                    hasBarLine = true;
                }
                else
                {
                    barSize = barNode.Get<Measurement>("value");
                    hasBarLine = barSize.Number > 0;
                }

                // Find out if we want displaystyle, textstyle, etc.
                string size = null;
                var styl = args[3];
                if (styl.Type == "ordgroup")
                {
                    if (styl.List("body").Count > 0)
                    {
                        var textOrd = ParseNodeUtil.AssertNodeType(styl.List("body")[0], "textord");
                        size = StylArray[int.Parse(textOrd.Str("text"), CultureInfo.InvariantCulture)];
                    }
                }
                else
                {
                    styl = ParseNodeUtil.AssertNodeType(styl, "textord");
                    size = StylArray[int.Parse(styl.Str("text"), CultureInfo.InvariantCulture)];
                }

                return WrapWithStyle(new ParseNode("genfrac", context.Parser.Mode)
                {
                    ["numer"] = numer,
                    ["denom"] = denom,
                    ["continued"] = false,
                    ["hasBarLine"] = hasBarLine,
                    ["barSize"] = barSize,
                    ["leftDelim"] = leftDelim,
                    ["rightDelim"] = rightDelim,
                }, size);
            },
        });

        // \above is an infix fraction that also defines a fraction bar size.
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "infix",
            Names = new List<string> { "\\above" },
            NumArgs = 1,
            ArgTypes = new List<string> { "size" },
            Infix = true,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("infix", context.Parser.Mode)
                {
                    ["replaceWith"] = "\\\\abovefrac",
                    ["size"] = ParseNodeUtil.AssertNodeType(args[0], "size").Get<Measurement>("value"),
                    ["token"] = context.Token,
                };
            },
        });

        Registries.DefineFunction(new FunctionSpec
        {
            Type = "genfrac",
            Names = new List<string> { "\\\\abovefrac" },
            NumArgs = 3,
            ArgTypes = new List<string> { "math", "size", "math" },

            Handler = (context, args, optArgs) =>
            {
                var numer = args[0];
                var barSize = ParseNodeUtil.AssertNodeType(args[1], "infix").Get<Measurement>("size");

                if (barSize == null)
                {
                    throw new InvalidOperationException(
                        $"\\\\abovefrac expected size, but got null");
                }

                var denom = args[2];

                var hasBarLine = barSize.Number > 0;
                return new ParseNode("genfrac", context.Parser.Mode)
                {
                    ["numer"] = numer,
                    ["denom"] = denom,
                    ["continued"] = false,
                    ["hasBarLine"] = hasBarLine,
                    ["barSize"] = barSize,
                    ["leftDelim"] = null,
                    ["rightDelim"] = null,
                };
            },
        });
    }
}

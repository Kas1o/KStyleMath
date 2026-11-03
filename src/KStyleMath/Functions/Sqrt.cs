namespace KStyleMath;

public static class SqrtFunctions
{
    public static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        // Square roots are handled in the TeXbook pg. 443, Rule 11.

        // First, we do the same steps as in overline to build the inner group
        // and line
        var inner = BuildHTML.BuildGroup(group.Node("body"), options.HavingCrampedStyle());
        if (inner.Height == 0)
        {
            // Render a small surd.
            inner.Height = options.FontMetrics().XHeight;
        }

        // Some groups can return document fragments.  Handle those by wrapping
        // them in a span.
        inner = BuildCommon.WrapFragment(inner, options);

        // Calculate the minimum size for the \surd delimiter
        var metrics = options.FontMetrics();
        var theta = metrics.DefaultRuleThickness;

        var phi = theta;
        if (options.Style.Id < Style.TEXT.Id)
        {
            phi = options.FontMetrics().XHeight;
        }

        // Calculate the clearance between the body and line
        var lineClearance = theta + phi / 4;

        var minDelimiterHeight = (inner.Height + inner.Depth +
            lineClearance + theta);

        // Create a sqrt SVG of the required minimum size
        var (img, ruleWidth, advanceWidth) =
            Delimiter.MakeSqrtImage(minDelimiterHeight, options);

        var delimDepth = img.Height - ruleWidth;

        // Adjust the clearance based on the delimiter size
        if (delimDepth > inner.Height + inner.Depth + lineClearance)
        {
            lineClearance =
                (lineClearance + delimDepth - inner.Height - inner.Depth) / 2;
        }

        // Shift the sqrt image
        var imgShift = img.Height - inner.Height - lineClearance - ruleWidth;

        inner.Style.PaddingLeft = Units.MakeEm(advanceWidth);

        // Overlay the image and the argument.
        var body = BuildCommon.MakeVList(VListParam.FirstBaseline(new List<object>
        {
            new VListElem
            {
                Elem = inner,
                WrapperClasses = new List<string> { "svg-align" },
            },
            new VListKern { Size = -(inner.Height + imgShift) },
            new VListElem { Elem = img },
            new VListKern { Size = ruleWidth },
        }), options);

        if (group.Node("index") == null)
        {
            return BuildCommon.MakeSpan(new List<string> { "mord", "sqrt" },
                new List<VirtualNode> { body }, options);
        }
        else
        {
            // Handle the optional root index

            // The index is always in scriptscript style
            var newOptions = options.HavingStyle(Style.SCRIPTSCRIPT);
            var rootm = BuildHTML.BuildGroup(group.Node("index"), newOptions, options);

            // The amount the index is shifted by. This is taken from the TeX
            // source, in the definition of `\r@@t`.
            var toShift = 0.6 * (body.Height - body.Depth);

            // Build a VList with the superscript shifted up correctly
            var rootVList = BuildCommon.MakeVList(VListParam.Shift(-toShift,
                new List<object>
                {
                    new VListElem { Elem = rootm },
                }), options);
            // Add a class surrounding it so we can add on the appropriate
            // kerning
            var rootVListWrap = BuildCommon.MakeSpan(new List<string> { "katex-root" },
                new List<VirtualNode> { rootVList });

            return BuildCommon.MakeSpan(new List<string> { "mord", "sqrt" },
                new List<VirtualNode> { rootVListWrap, body }, options);
        }
    }

    public static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var body = group.Node("body");
        var index = group.Node("index");
        return index != null
            ? new MathNode(
                "mroot", new List<VirtualNode>
                {
                    BuildMathML.BuildGroup(body, options),
                    BuildMathML.BuildGroup(index, options),
                })
            : new MathNode(
                "msqrt", new List<VirtualNode> { BuildMathML.BuildGroup(body, options) });
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "sqrt",
            Names = new List<string> { "\\sqrt" },
            NumArgs = 1,
            NumOptionalArgs = 1,

            Handler = (context, args, optArgs) =>
            {
                var index = optArgs[0];
                var body = args[0];
                return new ParseNode("sqrt", context.Parser.Mode)
                {
                    ["body"] = body,
                    ["index"] = index,
                };
            },

            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });
    }
}

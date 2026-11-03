namespace KStyleMath;

public static class RuleFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "rule",
            Names = new List<string> { "\\rule" },
            NumArgs = 2,
            NumOptionalArgs = 1,
            AllowedInText = true,
            AllowedInMath = true,
            ArgTypes = new List<string> { "size", "size", "size" },

            Handler = (context, args, optArgs) =>
            {
                var shift = optArgs[0];
                var width = ParseNodeUtil.AssertNodeType(args[0], "size");
                var height = ParseNodeUtil.AssertNodeType(args[1], "size");
                return new ParseNode("rule", context.Parser.Mode)
                {
                    ["shift"] = shift != null
                        ? ParseNodeUtil.AssertNodeType(shift, "size").Get<Measurement>("value")
                        : null,
                    ["width"] = width.Get<Measurement>("value"),
                    ["height"] = height.Get<Measurement>("value"),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                // Make an empty span for the rule
                var rule = BuildCommon.MakeSpan(
                    new List<string> { "mord", "katex-rule" },
                    new List<VirtualNode>(), options);

                // Calculate the shift, width, and height of the rule, and account for units
                double width = Units.CalculateSize(group.Get<Measurement>("width"), options);
                double height = Units.CalculateSize(group.Get<Measurement>("height"), options);
                double shift = group.Get<Measurement>("shift") != null
                    ? Units.CalculateSize(group.Get<Measurement>("shift"), options) : 0;

                // Style the rule to the right size
                rule.Style.BorderRightWidth = Units.MakeEm(width);
                rule.Style.BorderTopWidth = Units.MakeEm(height);
                rule.Style.Bottom = Units.MakeEm(shift);

                // Record the height and width
                rule.Width = width;
                rule.Height = height + shift;
                rule.Depth = -shift;
                // Font size is the number large enough that the browser will
                // reserve at least `absHeight` space above the baseline.
                // The 1.125 factor was empirically determined
                rule.MaxFontSize = height * 1.125 * options.SizeMultiplier;

                return rule;
            },
            MathmlBuilder = (group, options) =>
            {
                double width = Units.CalculateSize(group.Get<Measurement>("width"), options);
                double height = Units.CalculateSize(group.Get<Measurement>("height"), options);
                double shift = group.Get<Measurement>("shift") != null
                    ? Units.CalculateSize(group.Get<Measurement>("shift"), options) : 0;
                var color = options.Color != null && options.GetColor() != null
                    ? options.GetColor() : "black";

                var rule = new MathNode("mspace");
                rule.SetAttribute("mathbackground", color);
                rule.SetAttribute("width", Units.MakeEm(width));
                rule.SetAttribute("height", Units.MakeEm(height));

                var wrapper = new MathNode("mpadded",
                    new List<VirtualNode> { rule });
                if (shift >= 0)
                {
                    wrapper.SetAttribute("height", Units.MakeEm(shift));
                }
                else
                {
                    wrapper.SetAttribute("height", Units.MakeEm(shift));
                    wrapper.SetAttribute("depth", Units.MakeEm(-shift));
                }
                wrapper.SetAttribute("voffset", Units.MakeEm(shift));

                return wrapper;
            },
        });
    }
}

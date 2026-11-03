namespace KStyleMath;

// Horizontal spacing commands

// TODO: \hskip and \mskip should support plus and minus in lengths

public static class KernFunctions
{
    private static bool StrictTruthy(object strict)
    {
        if (strict == null) return false;
        if (strict is bool b) return b;
        if (strict is StrictMode m) return m != StrictMode.False;
        if (strict is string s) return s.Length > 0;
        return true;
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "kern",
            Names = new List<string> { "\\kern", "\\mkern", "\\hskip", "\\mskip" },
            NumArgs = 1,
            ArgTypes = new List<string> { "size" },
            Primitive = true,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var size = ParseNodeUtil.AssertNodeType(args[0], "size");
                var value = size.Get<Measurement>("value");
                if (StrictTruthy(parser.Settings.Strict))
                {
                    var mathFunction = (funcName[1] == 'm');  // \mkern, \mskip
                    var muUnit = (value.Unit == "mu");
                    if (mathFunction)
                    {
                        if (!muUnit)
                        {
                            StrictHelper.Handle(parser.Settings.Strict,
                                "mathVsTextUnits",
                                $"LaTeX's {funcName} supports only mu units, " +
                                $"not {value.Unit} units",
                                null, true);
                        }
                        if (parser.Mode != "math")
                        {
                            StrictHelper.Handle(parser.Settings.Strict,
                                "mathVsTextUnits",
                                $"LaTeX's {funcName} works only in math mode",
                                null, true);
                        }
                    }
                    else
                    {
                        // !mathFunction
                        if (muUnit)
                        {
                            StrictHelper.Handle(parser.Settings.Strict,
                                "mathVsTextUnits",
                                $"LaTeX's {funcName} doesn't support mu units",
                                null, true);
                        }
                    }
                }
                return new ParseNode("kern", parser.Mode)
                {
                    ["dimension"] = value,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                return BuildCommon.MakeGlue(group.Get<Measurement>("dimension"), options);
            },
            MathmlBuilder = (group, options) =>
            {
                var dimension = Units.CalculateSize(group.Get<Measurement>("dimension"), options);
                return new SpaceNode(dimension);
            },
        });
    }
}

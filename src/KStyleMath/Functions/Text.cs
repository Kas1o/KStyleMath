using System.Linq;

namespace KStyleMath;

// Non-mathy text, possibly in a font
public static class TextFunctions
{
    private static readonly Dictionary<string, string> TextFontFamilies = new(StringComparer.Ordinal)
    {
        ["\\text"] = null,
        ["\\textrm"] = "textrm",
        ["\\textsf"] = "textsf",
        ["\\texttt"] = "texttt",
        ["\\textnormal"] = "textrm",
    };

    private static readonly Dictionary<string, string> TextFontWeights = new(StringComparer.Ordinal)
    {
        ["\\textbf"] = "textbf",
        ["\\textmd"] = "textmd",
    };

    private static readonly Dictionary<string, string> TextFontShapes = new(StringComparer.Ordinal)
    {
        ["\\textit"] = "textit",
        ["\\textup"] = "textup",
    };

    private static Options OptionsWithFont(ParseNode group, Options options)
    {
        var font = group.Str("font");
        // Checks if the argument is a font family or a font style.
        if (string.IsNullOrEmpty(font))
        {
            return options;
        }
        else if (TextFontFamilies.TryGetValue(font, out var family) && family != null)
        {
            return options.WithTextFontFamily(family);
        }
        else if (TextFontWeights.TryGetValue(font, out var weight) && weight != null)
        {
            return options.WithTextFontWeight(weight);
        }
        else if (font == "\\emph")
        {
            return options.FontShape == "textit" ?
                options.WithTextFontShape("textup") :
                options.WithTextFontShape("textit");
        }

        return options.WithTextFontShape(
            TextFontShapes.TryGetValue(font, out var shape) ? shape : "");
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "text",
            Names = new List<string>
            {
                // Font families
                "\\text", "\\textrm", "\\textsf", "\\texttt", "\\textnormal",
                // Font weights
                "\\textbf", "\\textmd",
                // Font Shapes
                "\\textit", "\\textup", "\\emph",
            },
            NumArgs = 1,
            ArgTypes = new List<string> { "text" },
            AllowedInArgument = true,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var body = args[0];
                return new ParseNode("text", context.Parser.Mode)
                {
                    ["body"] = Registries.Ordargument(body),
                    ["font"] = context.FuncName,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var newOptions = OptionsWithFont(group, options);
                var inner = BuildHTML.BuildExpression(group.List("body"), newOptions, true);
                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "text" },
                    inner.Cast<VirtualNode>().ToList(), newOptions);
            },
            MathmlBuilder = (group, options) =>
            {
                var newOptions = OptionsWithFont(group, options);
                return BuildMathML.BuildExpressionRow(group.List("body"), newOptions);
            },
        });
    }
}

using System.Linq;

namespace KStyleMath;

public static class VerbFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "verb",
            Names = new List<string> { "\\verb" },
            NumArgs = 0,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                // \verb and \verb* are dealt with directly in Parser.js.
                // If we end up here, it's because of a failure to match the two delimiters
                // in the regex in Lexer.js.  LaTeX raises the following error when \verb is
                // terminated by end of line (or file).
                throw new ParseError(
                    "\\verb ended by end of line instead of matching delimiter");
            },

            HtmlBuilder = (group, options) =>
            {
                var text = MakeVerb(group);
                var body = new List<HtmlDomNode>();
                // \verb enters text mode and therefore is sized like \textstyle
                var newOptions = options.HavingStyle(options.Style.Text());
                for (int i = 0; i < text.Length; i++)
                {
                    var c = text[i].ToString();
                    if (c == "~")
                    {
                        c = "\\textasciitilde";
                    }
                    body.Add(BuildCommon.MakeSymbol(c, "Typewriter-Regular",
                        group.Mode, newOptions, new List<string> { "mord", "texttt" }));
                }
                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "text" }
                        .Concat(newOptions.SizingClasses(options)).ToList(),
                    BuildCommon.TryCombineChars(body.Cast<VirtualNode>().ToList()),
                    newOptions);
            },
            MathmlBuilder = (group, options) =>
            {
                var text = new MathTextNode(MakeVerb(group));
                var node = new MathNode("mtext", new List<VirtualNode> { text });
                node.SetAttribute("mathvariant", "monospace");
                return node;
            },
        });
    }

    /// <summary>
    /// Converts verb group into body string.
    ///
    /// \verb* replaces each space with an open box \u2423
    /// \verb replaces each space with a no-break space \xA0
    /// </summary>
    private static string MakeVerb(ParseNode group)
        => group.Str("body").Replace(" ", group.Bool("star") ? "\u2423" : "\u00A0");
}

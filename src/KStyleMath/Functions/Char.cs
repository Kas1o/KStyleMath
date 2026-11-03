using System.Globalization;
using System.Text.RegularExpressions;

namespace KStyleMath;

public static class CharFunctions
{
    private static readonly Regex ParseIntRegex = new(@"^\s*([+-]?\d+)", RegexOptions.Compiled);

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "textord",
            Names = new List<string> { "\\@char" },
            NumArgs = 1,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var arg = ParseNodeUtil.AssertNodeType(args[0], "ordgroup");
                var number = ParseNodeUtil.AssertCharacterGroup(
                    arg, "\\@char has non-numeric argument");
                var match = ParseIntRegex.Match(number);
                double code = match.Success
                    ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                    : double.NaN;
                string text;
                if (double.IsNaN(code))
                {
                    throw new ParseError($"\\@char has non-numeric argument {number}");
                    // If we drop IE support, the following code could be replaced with
                    // text = String.fromCodePoint(code)
                }
                else if (code < 0 || code > 0x10ffff)
                {
                    throw new ParseError($"\\@char with invalid code point {number}");
                }
                else if (code <= 0xffff)
                {
                    text = ((char)(int)code).ToString();
                }
                else
                {
                    // Astral code point; split into surrogate halves
                    code -= 0x10000;
                    int c = (int)code;
                    text = ((char)((c >> 10) + 0xd800)).ToString() +
                           ((char)((c & 0x3ff) + 0xdc00)).ToString();
                }
                return new ParseNode("textord", parser.Mode)
                {
                    ["text"] = text,
                };
            },
        });
    }
}

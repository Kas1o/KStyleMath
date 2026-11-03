using System.Text.RegularExpressions;

namespace KStyleMath;

public sealed class Lexer
{
    public string Input;
    public Settings Settings;
    public Regex TokenRegex;
    public Dictionary<string, int> Catcodes;

    private int _pos;

    public Lexer(string input, Settings settings)
    {
        Input = input;
        Settings = settings;
        TokenRegex = CachedTokenRegex;
        Catcodes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["%"] = 14,
            ["~"] = 13,
        };
    }

    public void SetCatcode(string ch, int code) => Catcodes[ch] = code;

    private static readonly Regex CachedTokenRegex = new(TokenRegexString, RegexOptions.Compiled);

    public static readonly Regex CombiningDiacriticalMarksEndRegex = new("[\u0300-\u036f]+$", RegexOptions.Compiled);

    private const string SpaceRegexString = "[ \r\n\t]";
    private const string ControlWordRegexString = "\\\\[a-zA-Z@]+";
    private const string ControlSymbolRegexString = "\\\\[^\uD800-\uDFFF]";
    private const string ControlSpaceRegexString = "\\\\(\n|[ \r\t]+\n?)[ \r\t]*";
    private const string CombiningDiacriticalMarkString = "[\u0300-\u036f]";

    private const string TokenRegexString =
        "(?<space>" + SpaceRegexString + "+)|" +
        "(?<controlSpace>" + ControlSpaceRegexString + ")|" +
        "(?<body>[!-\\[\\]-\u2027\u202A-\uD7FF\uF900-\uFFFF]" +
        CombiningDiacriticalMarkString + "*" +
        "|[\uD800-\uDBFF][\uDC00-\uDFFF]" +
        CombiningDiacriticalMarkString + "*" +
        "|\\\\verb\\*(?<verbStarChar>[\\s\\S]).*?\\k<verbStarChar>" +
        "|\\\\verb(?<verbChar>[^*a-zA-Z]).*?\\k<verbChar>" +
        "|(?<controlWord>" + ControlWordRegexString + ")" + SpaceRegexString + "*" +
        "|" + ControlSymbolRegexString + ")";

    public Token Lex()
    {
        var input = Input;
        int pos = _pos;
        if (pos == input.Length)
            return new Token("EOF", new SourceLocation(this, pos, pos));
        var match = TokenRegex.Match(input, pos);
        if (!match.Success || match.Index != pos)
            throw new ParseError($"Unexpected character: '{input[pos]}'",
                new Token(input[pos].ToString(), new SourceLocation(this, pos, pos + 1)));

        string text = NonEmpty(match.Groups["controlWord"].Value) ??
                      NonEmpty(match.Groups["body"].Value) ??
                      (match.Groups["controlSpace"].Success ? "\\ " : " ");
        _pos = pos + match.Length;

        if (Catcodes.TryGetValue(text, out var catcode) && catcode == 14)
        {
            int nlIndex = input.IndexOf('\n', _pos);
            if (nlIndex == -1)
            {
                _pos = input.Length;
                StrictHelper.Handle(Settings.Strict, "commentAtEnd",
                    "% comment has no terminating newline; LaTeX would fail because of " +
                    "commenting the end of math mode (e.g. $)", null, true);
            }
            else
            {
                _pos = nlIndex + 1;
            }
            return Lex();
        }

        return new Token(text, new SourceLocation(this, pos, _pos));
    }

    private static string NonEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}


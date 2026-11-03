namespace KStyleMath;

public static class ParseTree
{
    public static List<ParseNode> Parse(string toParse, Settings settings)
    {
        if (toParse == null) throw new ArgumentException("KaTeX can only parse string typed expression");
        var parser = new Parser(toParse, settings);
        parser.Gullet.Macros.Set("\\df@tag", null);
        var tree = parser.Parse();
        parser.Gullet.Macros.Set("\\current@color", null);
        parser.Gullet.Macros.Set("\\color", null);

        if (parser.Gullet.Macros.Get("\\df@tag") != null)
        {
            if (!settings.DisplayMode)
                throw new ParseError("\\tag works only in display equations");
            tree = new List<ParseNode>
            {
                new ParseNode("tag", "text")
                {
                    ["body"] = tree,
                    ["tag"] = parser.Subparse(new List<Token> { new Token("\\df@tag") }),
                },
            };
        }
        return tree;
    }
}

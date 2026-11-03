namespace KStyleMath;

public static class BuildTree
{
    private static Options OptionsFromSettings(Settings settings)
        => new Options(
            settings.DisplayMode ? Style.DISPLAY : Style.TEXT,
            maxSize: settings.MaxSize,
            minRuleThickness: settings.MinRuleThickness,
            strict: settings.Strict);

    private static VirtualNode DisplayWrap(VirtualNode node, Settings settings)
    {
        if (settings.DisplayMode)
        {
            var classes = new List<string> { "katex-display" };
            if (settings.Leqno) classes.Add("leqno");
            if (settings.Fleqn) classes.Add("fleqn");
            node = BuildCommon.MakeSpan(classes, new List<VirtualNode> { node });
        }
        return node;
    }

    public static VirtualNode Build(List<ParseNode> tree, string expression, Settings settings)
    {
        var options = OptionsFromSettings(settings);
        Span katexNode;
        if (settings.Output == "mathml")
        {
            return BuildMathML.Build(tree, expression, options, settings.DisplayMode, true);
        }
        else if (settings.Output == "html")
        {
            var htmlNode = BuildHTML.Build(tree, options);
            katexNode = BuildCommon.MakeSpan(new List<string> { "katex" }, new List<VirtualNode> { htmlNode });
        }
        else
        {
            var mathMLNode = BuildMathML.Build(tree, expression, options, settings.DisplayMode, false);
            var htmlNode = BuildHTML.Build(tree, options);
            katexNode = BuildCommon.MakeSpan(new List<string> { "katex" },
                new List<VirtualNode> { mathMLNode, htmlNode });
        }
        return (Span)DisplayWrap(katexNode, settings);
    }

    public static Span BuildHTMLTree(List<ParseNode> tree, Settings settings)
    {
        var options = OptionsFromSettings(settings);
        var htmlNode = BuildHTML.Build(tree, options);
        var katexNode = BuildCommon.MakeSpan(new List<string> { "katex" },
            new List<VirtualNode> { htmlNode });
        return (Span)DisplayWrap(katexNode, settings);
    }
}



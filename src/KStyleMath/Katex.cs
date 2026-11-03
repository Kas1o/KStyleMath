namespace KStyleMath;

/// <summary>Main KaTeX-compatible entry points.</summary>
public static class Katex
{
    public const string Version = "0.19.1";

    static Katex()
    {
        _ = MacrosData.Builtins;
        FunctionRegistry.DefineAll();
    }

    public static string RenderToString(string expression, Settings options = null)
    {
        var settings = options ?? new Settings();
        var node = RenderToDomTree(expression, settings);
        return node.ToMarkup();
    }

    public static string RenderToString(string expression, bool displayMode)
        => RenderToString(expression, new Settings { DisplayMode = displayMode });

    public static Span RenderToDomTree(string expression, Settings options = null)
    {
        var settings = options ?? new Settings();
        try
        {
            var tree = ParseTree.Parse(expression, settings);
            return (Span)BuildTree.Build(tree, expression, settings);
        }
        catch (Exception error)
        {
            if (settings.ThrowOnError || error is not ParseError) throw;
            return RenderError(error, expression, settings);
        }
    }

    public static Span RenderToHTMLTree(string expression, Settings options = null)
    {
        var settings = options ?? new Settings();
        try
        {
            var tree = ParseTree.Parse(expression, settings);
            return BuildTree.BuildHTMLTree(tree, settings);
        }
        catch (Exception error)
        {
            if (settings.ThrowOnError || error is not ParseError) throw;
            return RenderError(error, expression, settings);
        }
    }

    public static List<ParseNode> GenerateParseTree(string expression, Settings options = null)
        => ParseTree.Parse(expression, options ?? new Settings());

    public static Span RenderError(Exception error, string expression, Settings options)
    {
        var node = BuildCommon.MakeSpan(new List<string> { "katex-error" },
            new List<VirtualNode> { new SymbolNode(expression) });
        node.SetAttribute("title", error.ToString());
        node.SetAttribute("style", $"color:{options.ErrorColor}");
        return node;
    }
}




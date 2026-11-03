namespace KStyleMath;

public static class ParseNodeUtil
{
    public static ParseNode AssertNodeType(ParseNode node, string type)
    {
        if (node == null || node.Type != type)
            throw new InvalidOperationException(
                $"Expected node of type {type}, but got " +
                (node != null ? $"node of type {node.Type}" : "null"));
        return node;
    }

    public static ParseNode CheckSymbolNodeType(ParseNode node)
    {
        if (node != null && (node.Type == "atom" || NonAtoms.Contains(node.Type))) return node;
        return null;
    }

    public static ParseNode AssertSymbolNodeType(ParseNode node)
    {
        var typed = CheckSymbolNodeType(node);
        if (typed == null)
            throw new InvalidOperationException(
                "Expected node of symbol group type, but got " +
                (node != null ? $"node of type {node.Type}" : "null"));
        return typed;
    }

    private static readonly HashSet<string> NonAtoms = new(StringComparer.Ordinal)
        { "accent-token", "mathord", "op-token", "spacing", "textord" };

    public static string AssertCharacterGroup(ParseNode group, string errorMessage, bool allowSpaces = false)
    {
        string text = "";
        foreach (var node in group.List("body"))
        {
            if (node.Type == "textord")
                text += node.Str("text");
            else if (allowSpaces && node.Type == "spacing" && node.Str("text") == " ")
                text += " ";
            else
                throw new ParseError(errorMessage, group);
        }
        return text;
    }
}

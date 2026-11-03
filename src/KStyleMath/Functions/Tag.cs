namespace KStyleMath;

public static class TagFunctions
{
    private static MathNode Pad()
    {
        var padNode = new MathNode("mtd", new List<VirtualNode>());
        padNode.SetAttribute("width", "50%");
        return padNode;
    }

    public static void Define()
    {
        Registries.DefineFunctionBuilders(
            "tag",
            null,
            (group, options) =>
            {
                var table = new MathNode("mtable", new List<VirtualNode>
                {
                    new MathNode("mtr", new List<VirtualNode>
                    {
                        Pad(),
                        new MathNode("mtd", new List<VirtualNode>
                        {
                            BuildMathML.BuildExpressionRow(group.List("body"), options),
                        }),
                        Pad(),
                        new MathNode("mtd", new List<VirtualNode>
                        {
                            BuildMathML.BuildExpressionRow(group.List("tag"), options),
                        }),
                    }),
                });
                table.SetAttribute("width", "100%");
                return table;

                // TODO: Left-aligned tags.
                // Currently, the group and options passed here do not contain
                // enough info to set tag alignment. `leqno` is in Settings but it is
                // not passed to Options. On the HTML side, leqno is
                // set by a CSS class applied in buildTree.js. That would have worked
                // in MathML if browsers supported <mlabeledtr>. Since they don't, we
                // need to rewrite the way this function is called.
            });
    }
}

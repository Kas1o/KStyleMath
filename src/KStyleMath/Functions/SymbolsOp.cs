namespace KStyleMath;

// Operator ParseNodes created in Parser.js from symbol Groups in src/symbols.js.
public static class SymbolsOpFunctions
{
    public static void Define()
    {
        Registries.DefineFunctionBuilders(
            "atom",
            (group, options) =>
            {
                return BuildCommon.Mathsym(
                    group.Str("text"), group.Mode, options,
                    new List<string> { "m" + group.Str("family") });
            },
            (group, options) =>
            {
                var node = new MathNode(
                    "mo",
                    new List<VirtualNode>
                    {
                        BuildMathML.MakeText(group.Str("text"), group.Mode),
                    });
                if (group.Str("family") == "bin")
                {
                    var variant = BuildMathML.GetVariant(group, options);
                    if (variant == "bold-italic")
                    {
                        node.SetAttribute("mathvariant", variant);
                    }
                }
                else if (group.Str("family") == "punct")
                {
                    node.SetAttribute("separator", "true");
                }
                else if (group.Str("family") == "open" || group.Str("family") == "close")
                {
                    // Delims built here should not stretch vertically.
                    // See delimsizing.js for stretchy delims.
                    node.SetAttribute("stretchy", "false");
                }
                return node;
            });
    }
}

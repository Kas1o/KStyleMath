using System.Text.RegularExpressions;

namespace KStyleMath;

// "mathord" and "textord" ParseNodes created in Parser.js from symbol Groups in
// src/symbols.js.
public static class SymbolsOrdFunctions
{
    private static readonly Dictionary<string, string> DefaultVariant = new(StringComparer.Ordinal)
    {
        ["mi"] = "italic",
        ["mn"] = "normal",
        ["mtext"] = "normal",
    };

    private static readonly Regex Digit = new("[0-9]", RegexOptions.Compiled);

    public static void Define()
    {
        Registries.DefineFunctionBuilders(
            "mathord",
            (group, options) => BuildCommon.MakeOrd(group, options),
            (group, options) =>
            {
                var node = new MathNode(
                    "mi",
                    new List<VirtualNode>
                    {
                        BuildMathML.MakeText(group.Str("text"), group.Mode, options),
                    });

                var variant = BuildMathML.GetVariant(group, options) ?? "italic";
                if (variant != DefaultVariant.GetValueOrDefault(node.Type))
                {
                    node.SetAttribute("mathvariant", variant);
                }
                return node;
            });

        Registries.DefineFunctionBuilders(
            "textord",
            (group, options) => BuildCommon.MakeOrd(group, options),
            (group, options) =>
            {
                var text = BuildMathML.MakeText(group.Str("text"), group.Mode, options);
                var variant = BuildMathML.GetVariant(group, options) ?? "normal";

                MathNode node;
                if (group.Mode == "text")
                {
                    node = new MathNode("mtext", new List<VirtualNode> { text });
                }
                else if (Digit.IsMatch(group.Str("text")))
                {
                    node = new MathNode("mn", new List<VirtualNode> { text });
                }
                else if (group.Str("text") == "\\prime")
                {
                    node = new MathNode("mo", new List<VirtualNode> { text });
                }
                else
                {
                    node = new MathNode("mi", new List<VirtualNode> { text });
                }
                if (variant != DefaultVariant.GetValueOrDefault(node.Type))
                {
                    node.SetAttribute("mathvariant", variant);
                }

                return node;
            });
    }
}


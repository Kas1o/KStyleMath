using System.Linq;

namespace KStyleMath;

public static class OrdgroupFunctions
{
    public static void Define()
    {
        Registries.DefineFunctionBuilders(
            "ordgroup",
            (group, options) =>
            {
                if (group.Bool("semisimple"))
                {
                    return BuildCommon.MakeFragment(
                        BuildHTML.BuildExpression(group.List("body"), options, false)
                            .Cast<VirtualNode>().ToList());
                }
                return BuildCommon.MakeSpan(
                    new List<string> { "mord" },
                    BuildHTML.BuildExpression(group.List("body"), options, true)
                        .Cast<VirtualNode>().ToList(),
                    options);
            },
            (group, options) => BuildMathML.BuildExpressionRow(group.List("body"), options, true));
    }
}

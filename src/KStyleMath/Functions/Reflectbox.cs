namespace KStyleMath;

public static class ReflectboxFunctions
{
    private static ParseNode Handler(FunctionContext context, List<ParseNode> args,
        List<ParseNode> optArgs)
        => new ParseNode("reflectbox", context.Parser.Mode)
        {
            ["body"] = args[0],
        };

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "reflectbox",
            Names = new List<string> { "\\reflectbox" },
            NumArgs = 1,
            ArgTypes = new List<string> { "hbox" },
            AllowedInText = true,

            Handler = Handler,

            HtmlBuilder = (group, options) =>
            {
                return BuildCommon.MakeSpan(
                    new List<string> { "mord", "reflectbox" },
                    new List<VirtualNode> { BuildHTML.BuildGroup(group.Node("body"), options) },
                    options);
            },
            MathmlBuilder = (group, options) =>
            {
                return BuildMathML.BuildGroup(group.Node("body"), options);
            },
        });

        // Parse math directly so the shared builders inherit the surrounding style.
        // \reflectbox instead uses an hbox argument for LaTeX's text-box behavior.
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "reflectbox",
            Names = new List<string> { "\\mathreflectbox" },
            NumArgs = 1,
            ArgTypes = new List<string> { "math" },
            Handler = Handler,
        });
    }
}

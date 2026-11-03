namespace KStyleMath;

public static class RelaxFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "internal",
            Names = new List<string> { "\\relax" },
            NumArgs = 0,
            AllowedInText = true,
            AllowedInArgument = true,

            Handler = (context, args, optArgs) =>
            {
                return new ParseNode("internal", context.Parser.Mode);
            },
        });
    }
}

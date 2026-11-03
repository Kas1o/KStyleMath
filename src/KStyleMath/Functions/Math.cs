namespace KStyleMath;

// Switching from text mode back to math mode

public static class MathFunctions
{
    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "styling",
            Names = new List<string> { "\\(", "$" },
            NumArgs = 0,
            AllowedInText = true,
            AllowedInMath = false,

            Handler = (context, args, optArgs) =>
            {
                var funcName = context.FuncName;
                var parser = context.Parser;
                var outerMode = parser.Mode;
                parser.SwitchMode("math");
                var close = (funcName == "\\(" ? "\\)" : "$");
                var body = parser.ParseExpression(false, close);
                parser.Expect(close);
                parser.SwitchMode(outerMode);
                return new ParseNode("styling", parser.Mode)
                {
                    ["style"] = "text",
                    ["resetFont"] = true,
                    ["body"] = body,
                };
            },
        });

        // Check for extra closing math delimiters
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "text", // Doesn't matter what this is.
            Names = new List<string> { "\\)", "\\]" },
            NumArgs = 0,
            AllowedInText = true,
            AllowedInMath = false,

            Handler = (context, args, optArgs) =>
            {
                throw new ParseError($"Mismatched {context.FuncName}");
            },
        });
    }
}

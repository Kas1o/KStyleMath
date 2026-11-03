namespace KStyleMath;

public static class EnvironmentFunctions
{
    public static void Define()
    {
        // Environment delimiters. HTML/MathML rendering is defined in the corresponding
        // defineEnvironment definitions.
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "environment",
            Names = new List<string> { "\\begin", "\\end" },
            NumArgs = 1,
            ArgTypes = new List<string> { "text" },

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var nameGroup = args[0];
                if (nameGroup.Type != "ordgroup")
                {
                    throw new ParseError("Invalid environment name", nameGroup);
                }
                var envName = ParseNodeUtil.AssertCharacterGroup(
                    nameGroup,
                    "Environment name should contain only text characters and spaces",
                    true);

                if (funcName == "\\begin")
                {
                    // begin...end is similar to left...right
                    if (!Registries.Environments.ContainsKey(envName))
                    {
                        throw new ParseError(
                            "No such environment: " + envName, nameGroup);
                    }
                    // Build the environment object. Arguments and other information will
                    // be made available to the begin and end methods using properties.
                    var env = Registries.Environments[envName];
                    var (envArgs, envOptArgs) =
                        parser.ParseArguments("\\begin{" + envName + "}", env);
                    var envContext = new EnvContext
                    {
                        Mode = parser.Mode,
                        EnvName = envName,
                        Parser = parser,
                    };
                    var result = env.Handler(envContext, envArgs, envOptArgs);
                    parser.Expect("\\end", false);
                    var endNameToken = parser.NextToken;
                    var end = ParseNodeUtil.AssertNodeType(parser.ParseFunction(), "environment");
                    if (end.Str("name") != envName)
                    {
                        throw new ParseError(
                            $"Mismatch: \\begin{{{envName}}} matched by \\end{{{end.Str("name")}}}",
                            endNameToken);
                    }
                    // env.handler returns the specific node type (e.g. "array"),
                    // not "environment". This cast is unavoidable: defineFunction
                    // requires the handler to return ParseNode<"environment"> but
                    // \begin delegates to environment handlers with different types.
                    return result;
                }

                return new ParseNode("environment", parser.Mode)
                {
                    ["name"] = envName,
                    ["nameGroup"] = nameGroup,
                };
            },
        });
    }
}

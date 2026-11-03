namespace KStyleMath;

public sealed class FunctionContext
{
    public string FuncName;
    public Parser Parser;
    public Token Token;
    public string BreakOnTokenText;
}

public sealed class EnvContext
{
    public string Mode;
    public string EnvName;
    public Parser Parser;
}

public sealed class FunctionSpec
{
    public string Type;
    public List<string> Names = new();
    public int NumArgs;
    public List<string> ArgTypes;
    public bool AllowedInArgument;
    public bool AllowedInText;
    public bool AllowedInMath = true;
    public int NumOptionalArgs;
    public bool Infix;
    public bool Primitive;
    public Func<FunctionContext, List<ParseNode>, List<ParseNode>, ParseNode> Handler;
    public Func<ParseNode, Options, VirtualNode> HtmlBuilder;
    public Func<ParseNode, Options, VirtualNode> MathmlBuilder;
}

public sealed class EnvSpec
{
    public List<string> Names = new();
    public string Type;
    public int NumArgs;
    public List<string> ArgTypes;
    public bool AllowedInText;
    public int NumOptionalArgs;
    public Func<EnvContext, List<ParseNode>, List<ParseNode>, ParseNode> Handler;
    public Func<ParseNode, Options, VirtualNode> HtmlBuilder;
    public Func<ParseNode, Options, VirtualNode> MathmlBuilder;
}

public static class Registries
{
    public static readonly Dictionary<string, FunctionSpec> Functions = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, EnvSpec> Environments = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, Func<ParseNode, Options, VirtualNode>> HtmlGroupBuilders = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, Func<ParseNode, Options, VirtualNode>> MathmlGroupBuilders = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, ParserFunction> ParserFunctions = new(StringComparer.Ordinal);

    public static void DefineFunction(FunctionSpec spec)
    {
        spec.Names ??= new List<string>();
        foreach (var name in spec.Names) Functions[name] = spec;
        if (!string.IsNullOrEmpty(spec.Type))
        {
            if (spec.HtmlBuilder != null) HtmlGroupBuilders[spec.Type] = spec.HtmlBuilder;
            if (spec.MathmlBuilder != null) MathmlGroupBuilders[spec.Type] = spec.MathmlBuilder;
        }
    }

    public static void DefineFunctionBuilders(string type,
        Func<ParseNode, Options, VirtualNode> htmlBuilder,
        Func<ParseNode, Options, VirtualNode> mathmlBuilder)
    {
        if (htmlBuilder != null) HtmlGroupBuilders[type] = htmlBuilder;
        if (mathmlBuilder != null) MathmlGroupBuilders[type] = mathmlBuilder;
    }

    public static void DefineEnvironment(EnvSpec spec)
    {
        foreach (var name in spec.Names)
            Environments[name] = new EnvSpec
            {
                Type = spec.Type,
                NumArgs = spec.NumArgs,
                ArgTypes = spec.ArgTypes,
                AllowedInText = false,
                NumOptionalArgs = 0,
                Handler = spec.Handler,
                HtmlBuilder = spec.HtmlBuilder,
                MathmlBuilder = spec.MathmlBuilder,
            };
        if (!string.IsNullOrEmpty(spec.Type))
        {
            if (spec.HtmlBuilder != null) HtmlGroupBuilders[spec.Type] = spec.HtmlBuilder;
            if (spec.MathmlBuilder != null) MathmlGroupBuilders[spec.Type] = spec.MathmlBuilder;
        }
    }

    public static ParseNode NormalizeArgument(ParseNode arg)
        => arg.Type == "ordgroup" && arg.List("body").Count == 1 ? arg.List("body")[0] : arg;

    public static List<ParseNode> Ordargument(ParseNode arg)
        => arg.Type == "ordgroup" ? arg.List("body") : new List<ParseNode> { arg };
}

public delegate ParseNode ParserFunction(Parser parser, FunctionContext context, List<ParseNode> args, List<ParseNode> optArgs);


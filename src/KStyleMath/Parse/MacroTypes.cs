namespace KStyleMath;

public interface MacroContextInterface
{
    string Mode { get; }
    Namespace<object> Macros { get; }
    Token Future();
    Token PopToken();
    void ConsumeSpaces();
    object ExpandOnce(bool expandableOnly = false);
    Token ExpandAfterFuture();
    Token ExpandNextToken();
    List<Token> ExpandMacro(string name);
    string ExpandMacroAsText(string name);
    List<Token> ExpandTokens(List<Token> tokens);
    MacroArg ConsumeArg(List<string> delims = null);
    List<List<Token>> ConsumeArgs(int numArgs, List<List<string>> delimiters = null);
    bool IsDefined(string name);
    bool IsExpandable(string name);
}

public sealed class MacroArg
{
    public List<Token> Tokens;
    public Token Start;
    public Token End;
}

public sealed class MacroExpansion
{
    public List<Token> Tokens;
    public int NumArgs;
    public List<List<string>> Delimiters;
    public bool Unexpandable;
}

public delegate object MacroFunction(MacroContextInterface ctx);

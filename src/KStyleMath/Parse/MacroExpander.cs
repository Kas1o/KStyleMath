using System.Text.RegularExpressions;

namespace KStyleMath;

public sealed class MacroExpander : MacroContextInterface
{
    public Settings Settings;
    public int ExpansionCount;
    public Lexer Lexer;
    public Namespace<object> Macros;
    public List<Token> Stack;
    public string Mode { get; set; }
    Namespace<object> MacroContextInterface.Macros => Macros;

    public static readonly Dictionary<string, bool> ImplicitCommands = new(StringComparer.Ordinal)
    {
        ["^"] = true,
        ["_"] = true,
        ["\\limits"] = true,
        ["\\nolimits"] = true,
    };

    public MacroExpander(string input, Settings settings, string mode)
    {
        Settings = settings;
        ExpansionCount = 0;
        Feed(input);
        Macros = new Namespace<object>(MacrosData.Builtins, settings.Macros);
        Mode = mode;
        Stack = new List<Token>();
    }

    public void Feed(string input) => Lexer = new Lexer(input, Settings);

    public void SwitchMode(string newMode) => Mode = newMode;

    public void BeginGroup() => Macros.BeginGroup();
    public void EndGroup() => Macros.EndGroup();
    public void EndGroups() => Macros.EndGroups();

    public Token Future()
    {
        if (Stack.Count == 0) PushToken(Lexer.Lex());
        return Stack[^1];
    }

    public Token PopToken()
    {
        Future();
        var token = Stack[^1];
        Stack.RemoveAt(Stack.Count - 1);
        return token;
    }

    public void PushToken(Token token) => Stack.Add(token);
    public void PushTokens(List<Token> tokens) => Stack.AddRange(tokens);

    public Token ScanArgument(bool isOptional)
    {
        Token start;
        Token end;
        List<Token> tokens;
        if (isOptional)
        {
            ConsumeSpaces();
            if (Future().Text != "[") return null;
            start = PopToken();
            var arg = ConsumeArg(new List<string> { "]" });
            tokens = arg.Tokens;
            end = arg.End;
        }
        else
        {
            var arg = ConsumeArg();
            tokens = arg.Tokens;
            start = arg.Start;
            end = arg.End;
        }

        PushToken(new Token("EOF", end.Loc));
        PushTokens(tokens);
        return new Token("", SourceLocation.Range(start, end));
    }

    public void ConsumeSpaces()
    {
        while (true)
        {
            var token = Future();
            if (token.Text == " ") Stack.RemoveAt(Stack.Count - 1);
            else break;
        }
    }

    public MacroArg ConsumeArg(List<string> delims = null)
    {
        var tokens = new List<Token>();
        bool isDelimited = delims != null && delims.Count > 0;
        if (!isDelimited) ConsumeSpaces();
        var start = Future();
        Token tok = null;
        int depth = 0;
        int match = 0;
        do
        {
            tok = PopToken();
            tokens.Add(tok);
            if (tok.Text == "{") ++depth;
            else if (tok.Text == "}")
            {
                --depth;
                if (depth == -1) throw new ParseError("Extra }", tok);
            }
            else if (tok.Text == "EOF")
            {
                throw new ParseError("Unexpected end of input in a macro argument" +
                                     ", expected '" + (delims != null && isDelimited ? delims[match] : "}") + "'", tok);
            }
            if (delims != null && isDelimited)
            {
                if ((depth == 0 || (depth == 1 && delims[match] == "{")) && tok.Text == delims[match])
                {
                    ++match;
                    if (match == delims.Count)
                    {
                        tokens.RemoveRange(tokens.Count - match, match);
                        break;
                    }
                }
                else
                {
                    match = 0;
                }
            }
        } while (depth != 0 || isDelimited);

        if (start.Text == "{" && tokens.Count > 0 && tokens[^1].Text == "}")
        {
            tokens.RemoveAt(tokens.Count - 1);
            tokens.RemoveAt(0);
        }
        tokens.Reverse();
        return new MacroArg { Tokens = tokens, Start = start, End = tok };
    }

    public List<List<Token>> ConsumeArgs(int numArgs, List<List<string>> delimiters = null)
    {
        if (delimiters != null)
        {
            if (delimiters.Count != numArgs + 1)
                throw new ParseError("The length of delimiters doesn't match the number of args!");
            var delims = delimiters[0];
            for (int i = 0; i < delims.Count; i++)
            {
                var tok = PopToken();
                if (delims[i] != tok.Text)
                    throw new ParseError("Use of the macro doesn't match its definition", tok);
            }
        }

        var args = new List<List<Token>>();
        for (int i = 0; i < numArgs; i++)
            args.Add(ConsumeArg(delimiters?[i + 1]).Tokens);
        return args;
    }

    public void CountExpansion(int amount)
    {
        ExpansionCount += amount;
        if (ExpansionCount > Settings.MaxExpand)
            throw new ParseError("Too many expansions: infinite loop or need to increase maxExpand setting");
    }

    public object ExpandOnce(bool expandableOnly = false)
    {
        var topToken = PopToken();
        var name = topToken.Text;
        var expansion = topToken.Noexpand != true ? GetExpansion(name) : null;
        if (expansion == null || (expandableOnly && expansion.Unexpandable))
        {
            if (expandableOnly && expansion == null && name.StartsWith("\\") && !IsDefined(name))
                throw new ParseError("Undefined control sequence: " + name);
            PushToken(topToken);
            return false;
        }
        CountExpansion(1);
        var tokens = expansion.Tokens;
        var args = ConsumeArgs(expansion.NumArgs, expansion.Delimiters);
        if (expansion.NumArgs > 0)
        {
            tokens = new List<Token>(tokens);
            for (int i = tokens.Count - 1; i >= 0; --i)
            {
                var tok = tokens[i];
                if (tok.Text == "#")
                {
                    if (i == 0) throw new ParseError("Incomplete placeholder at end of macro body", tok);
                    tok = tokens[--i];
                    if (tok.Text == "#")
                    {
                        tokens.RemoveAt(i + 1);
                    }
                    else if (Regex.IsMatch(tok.Text, "^[1-9]$"))
                    {
                        int idx = tok.Text[0] - '1';
                        tokens.RemoveRange(i, 2);
                        tokens.InsertRange(i, args[idx]);
                    }
                    else
                    {
                        throw new ParseError("Not a valid argument number", tok);
                    }
                }
            }
        }
        PushTokens(tokens);
        return tokens.Count;
    }

    public Token ExpandAfterFuture()
    {
        ExpandOnce();
        return Future();
    }

    public Token ExpandNextToken()
    {
        while (true)
        {
            if (ExpandOnce() is bool b && b == false)
            {
                var token = PopToken();
                if (token.TreatAsRelax == true) token.Text = "\\relax";
                return token;
            }
        }
    }

    public List<Token> ExpandMacro(string name)
        => Macros.Has(name) ? ExpandTokens(new List<Token> { new Token(name) }) : null;

    public List<Token> ExpandTokens(List<Token> tokens)
    {
        var output = new List<Token>();
        int oldStackLength = Stack.Count;
        PushTokens(tokens);
        while (Stack.Count > oldStackLength)
        {
            if (ExpandOnce(true) is bool b && b == false)
            {
                var token = PopToken();
                if (token.TreatAsRelax == true)
                {
                    token.Noexpand = false;
                    token.TreatAsRelax = false;
                }
                output.Add(token);
            }
        }
        CountExpansion(output.Count);
        return output;
    }

    public string ExpandMacroAsText(string name)
    {
        var tokens = ExpandMacro(name);
        return tokens != null ? string.Concat(tokens.Select(t => t.Text)) : null;
    }

    public MacroExpansion GetExpansion(string name)
    {
        var definition = Macros.Get(name);
        if (definition == null) return null;
        if (name.Length == 1)
        {
            if (Lexer.Catcodes.TryGetValue(name, out var catcode) && catcode != 13) return null;
        }
        object expansion = definition is MacroFunction fn ? fn(this) : definition;
        if (expansion is string str)
        {
            int numArgs = 0;
            if (str.Contains('#'))
            {
                var stripped = str.Replace("##", "");
                while (stripped.Contains("#" + (numArgs + 1))) ++numArgs;
            }
            var bodyLexer = new Lexer(str, Settings);
            var tokens = new List<Token>();
            var tok = bodyLexer.Lex();
            while (tok.Text != "EOF")
            {
                tokens.Add(tok);
                tok = bodyLexer.Lex();
            }
            tokens.Reverse();
            return new MacroExpansion { Tokens = tokens, NumArgs = numArgs };
        }
        return expansion as MacroExpansion;
    }

    public bool IsDefined(string name)
        => Macros.Has(name) || Registries.Functions.ContainsKey(name) ||
           Symbols.Get("math", name) != null || Symbols.Get("text", name) != null ||
           ImplicitCommands.ContainsKey(name);

    public bool IsExpandable(string name)
    {
        var macro = Macros.Get(name);
        if (macro != null)
            return macro is string || macro is MacroFunction || !(macro is MacroExpansion me && me.Unexpandable);
        return Registries.Functions.TryGetValue(name, out var f) && !f.Primitive;
    }
}

public static class UnicodeSupOrSub
{
    public static readonly Regex UnicodeSubRegEx = new(
        "^[₊₋₌₍₎₀₁₂₃₄₅₆₇₈₉ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓᵦᵧᵨᵩᵪ]");

    public static readonly Dictionary<string, string> USubsAndSups = new(StringComparer.Ordinal)
    {
        ["₊"] = "+", ["₋"] = "-", ["₌"] = "=", ["₍"] = "(", ["₎"] = ")",
        ["₀"] = "0", ["₁"] = "1", ["₂"] = "2", ["₃"] = "3", ["₄"] = "4",
        ["₅"] = "5", ["₆"] = "6", ["₇"] = "7", ["₈"] = "8", ["₉"] = "9",
        ["\u2090"] = "a", ["\u2091"] = "e", ["\u2095"] = "h", ["\u1D62"] = "i",
        ["\u2C7C"] = "j", ["\u2096"] = "k", ["\u2097"] = "l", ["\u2098"] = "m",
        ["\u2099"] = "n", ["\u2092"] = "o", ["\u209A"] = "p", ["\u1D63"] = "r",
        ["\u209B"] = "s", ["\u209C"] = "t", ["\u1D64"] = "u", ["\u1D65"] = "v",
        ["\u2093"] = "x", ["\u1D66"] = "β", ["\u1D67"] = "γ", ["\u1D68"] = "ρ",
        ["\u1D69"] = "\u03d5", ["\u1D6A"] = "χ",
        ["⁺"] = "+", ["⁻"] = "-", ["⁼"] = "=", ["⁽"] = "(", ["⁾"] = ")",
        ["⁰"] = "0", ["¹"] = "1", ["²"] = "2", ["³"] = "3", ["⁴"] = "4",
        ["⁵"] = "5", ["⁶"] = "6", ["⁷"] = "7", ["⁸"] = "8", ["⁹"] = "9",
        ["\u1D2C"] = "A", ["\u1D2E"] = "B", ["\u1D30"] = "D", ["\u1D31"] = "E",
        ["\u1D33"] = "G", ["\u1D34"] = "H", ["\u1D35"] = "I", ["\u1D36"] = "J",
        ["\u1D37"] = "K", ["\u1D38"] = "L", ["\u1D39"] = "M", ["\u1D3A"] = "N",
        ["\u1D3C"] = "O", ["\u1D3E"] = "P", ["\u1D3F"] = "R", ["\u1D40"] = "T",
        ["\u1D41"] = "U", ["\u2C7D"] = "V", ["\u1D42"] = "W", ["\u1D43"] = "a",
        ["\u1D47"] = "b", ["\u1D9C"] = "c", ["\u1D48"] = "d", ["\u1D49"] = "e",
        ["\u1DA0"] = "f", ["\u1D4D"] = "g", ["\u02B0"] = "h", ["\u2071"] = "i",
        ["\u02B2"] = "j", ["\u1D4F"] = "k", ["\u02E1"] = "l", ["\u1D50"] = "m",
        ["\u207F"] = "n", ["\u1D52"] = "o", ["\u1D56"] = "p", ["\u02B3"] = "r",
        ["\u02E2"] = "s", ["\u1D57"] = "t", ["\u1D58"] = "u", ["\u1D5B"] = "v",
        ["\u02B7"] = "w", ["\u02E3"] = "x", ["\u02B8"] = "y", ["\u1DBB"] = "z",
        ["\u1D5D"] = "β", ["\u1D5E"] = "γ", ["\u1D5F"] = "δ", ["\u1D60"] = "\u03d5",
        ["\u1D61"] = "χ", ["\u1DBF"] = "θ",
    };
}

using System.Text.RegularExpressions;

namespace KStyleMath;

public sealed class Parser
{
    public string Mode;
    public MacroExpander Gullet;
    public Settings Settings;
    public int LeftrightDepth;
    public Token NextToken;

    public Parser(string input, Settings settings)
    {
        Mode = "math";
        Gullet = new MacroExpander(input, settings, Mode);
        Settings = settings;
        LeftrightDepth = 0;
        NextToken = null;
    }

    public void Expect(string text, bool consume = true)
    {
        if (Fetch().Text != text)
            throw new ParseError($"Expected '{text}', got '{Fetch().Text}'", Fetch());
        if (consume) Consume();
    }

    public void Consume() => NextToken = null;

    public Token Fetch()
    {
        NextToken ??= Gullet.ExpandNextToken();
        return NextToken;
    }

    public void SwitchMode(string newMode)
    {
        Mode = newMode;
        Gullet.SwitchMode(newMode);
    }

    public List<ParseNode> Parse()
    {
        if (!Settings.GlobalGroup) Gullet.BeginGroup();
        if (Settings.ColorIsTextColor) Gullet.Macros.Set("\\color", "\\textcolor");
        try
        {
            var parse = ParseExpression(false);
            Expect("EOF");
            if (!Settings.GlobalGroup) Gullet.EndGroup();
            return parse;
        }
        finally
        {
            Gullet.EndGroups();
        }
    }

    public List<ParseNode> Subparse(List<Token> tokens)
    {
        var oldToken = NextToken;
        Consume();
        Gullet.PushToken(new Token("}"));
        Gullet.PushTokens(tokens);
        var parse = ParseExpression(false);
        Expect("}");
        NextToken = oldToken;
        return parse;
    }

    public static readonly HashSet<string> EndOfExpression = new(StringComparer.Ordinal)
        { "}", "\\endgroup", "\\end", "\\right", "&" };

    public List<ParseNode> ParseExpression(bool breakOnInfix, string breakOnTokenText = null)
    {
        var body = new List<ParseNode>();
        while (true)
        {
            if (Mode == "math") ConsumeSpaces();
            var lex = Fetch();
            if (EndOfExpression.Contains(lex.Text)) break;
            if (breakOnTokenText != null && lex.Text == breakOnTokenText) break;
            if (breakOnInfix && Registries.Functions.TryGetValue(lex.Text, out var f) && f.Infix) break;
            var atom = ParseAtom(breakOnTokenText);
            if (atom == null) break;
            if (atom.Type == "internal") continue;
            body.Add(atom);
        }
        if (Mode == "text") FormLigatures(body);
        return HandleInfixNodes(body);
    }

    public List<ParseNode> HandleInfixNodes(List<ParseNode> body)
    {
        int overIndex = -1;
        string funcName = null;
        for (int i = 0; i < body.Count; i++)
        {
            var node = body[i];
            if (node.Type == "infix")
            {
                if (overIndex != -1) throw new ParseError("only one infix operator per group", node.Get<Token>("token"));
                overIndex = i;
                funcName = node.Str("replaceWith");
            }
        }
        if (overIndex != -1 && funcName != null)
        {
            ParseNode numerNode;
            ParseNode denomNode;
            var numerBody = body.GetRange(0, overIndex);
            var denomBody = body.GetRange(overIndex + 1, body.Count - overIndex - 1);
            if (numerBody.Count == 1 && numerBody[0].Type == "ordgroup") numerNode = numerBody[0];
            else numerNode = new ParseNode("ordgroup", Mode) { ["body"] = numerBody };
            if (denomBody.Count == 1 && denomBody[0].Type == "ordgroup") denomNode = denomBody[0];
            else denomNode = new ParseNode("ordgroup", Mode) { ["body"] = denomBody };

            ParseNode node;
            if (funcName == "\\\\abovefrac")
                node = CallFunction(funcName, new List<ParseNode> { numerNode, body[overIndex], denomNode }, new List<ParseNode>());
            else
                node = CallFunction(funcName, new List<ParseNode> { numerNode, denomNode }, new List<ParseNode>());
            return new List<ParseNode> { node };
        }
        return body;
    }

    public ParseNode HandleSupSubscript(string name)
    {
        var symbolToken = Fetch();
        var symbol = symbolToken.Text;
        Consume();
        ConsumeSpaces();
        ParseNode group;
        do
        {
            group = ParseGroup(name);
        } while (group?.Type == "internal");
        if (group == null)
            throw new ParseError("Expected group after '" + symbol + "'", symbolToken);
        return group;
    }

    public ParseNode FormatUnsupportedCmd(string text)
    {
        var textordArray = new List<ParseNode>();
        for (int i = 0; i < text.Length; i++)
            textordArray.Add(new ParseNode("textord", "text") { ["text"] = text[i].ToString() });
        var textNode = new ParseNode("text", Mode) { ["body"] = textordArray };
        return new ParseNode("color", Mode)
        {
            ["color"] = Settings.ErrorColor,
            ["body"] = new List<ParseNode> { textNode },
        };
    }

    public ParseNode ParseAtom(string breakOnTokenText = null)
    {
        var baseNode = ParseGroup("atom", breakOnTokenText);
        if (baseNode?.Type == "internal") return baseNode;
        if (Mode == "text") return baseNode;

        ParseNode superscript = null;
        ParseNode subscript = null;
        while (true)
        {
            ConsumeSpaces();
            var lex = Fetch();
            if (lex.Text == "\\limits" || lex.Text == "\\nolimits")
            {
                if (baseNode != null && baseNode.Type == "op")
                {
                    baseNode["limits"] = lex.Text == "\\limits";
                    baseNode["alwaysHandleSupSub"] = true;
                }
                else if (baseNode != null && baseNode.Type == "operatorname")
                {
                    if (baseNode.Bool("alwaysHandleSupSub"))
                        baseNode["limits"] = lex.Text == "\\limits";
                }
                else
                {
                    throw new ParseError("Limit controls must follow a math operator", lex);
                }
                Consume();
            }
            else if (lex.Text == "^")
            {
                if (superscript != null) throw new ParseError("Double superscript", lex);
                superscript = HandleSupSubscript("superscript");
            }
            else if (lex.Text == "_")
            {
                if (subscript != null) throw new ParseError("Double subscript", lex);
                subscript = HandleSupSubscript("subscript");
            }
            else if (lex.Text == "'")
            {
                if (superscript != null) throw new ParseError("Double superscript", lex);
                var prime = new ParseNode("textord", Mode) { ["text"] = "\\prime" };
                var primes = new List<ParseNode> { prime };
                Consume();
                while (Fetch().Text == "'")
                {
                    primes.Add(prime);
                    Consume();
                }
                if (Fetch().Text == "^") primes.Add(HandleSupSubscript("superscript"));
                superscript = new ParseNode("ordgroup", Mode) { ["body"] = primes };
            }
            else if (UnicodeSupOrSub.USubsAndSups.ContainsKey(lex.Text))
            {
                bool isSub = UnicodeSupOrSub.UnicodeSubRegEx.IsMatch(lex.Text);
                var subsupTokens = new List<Token> { new Token(UnicodeSupOrSub.USubsAndSups[lex.Text]) };
                Consume();
                while (true)
                {
                    var token = Fetch().Text;
                    if (!UnicodeSupOrSub.USubsAndSups.ContainsKey(token)) break;
                    if (UnicodeSupOrSub.UnicodeSubRegEx.IsMatch(token) != isSub) break;
                    subsupTokens.Insert(0, new Token(UnicodeSupOrSub.USubsAndSups[token]));
                    Consume();
                }
                var body = Subparse(subsupTokens);
                if (isSub)
                    subscript = new ParseNode("ordgroup", "math") { ["body"] = body };
                else
                    superscript = new ParseNode("ordgroup", "math") { ["body"] = body };
            }
            else
            {
                break;
            }
        }

        if (superscript != null && subscript != null)
            return new ParseNode("supsub", Mode)
            {
                ["base"] = baseNode, ["sup"] = superscript, ["sub"] = subscript,
            };
        if (superscript != null)
            return new ParseNode("supsub", Mode) { ["base"] = baseNode, ["sup"] = superscript };
        if (subscript != null)
            return new ParseNode("supsub", Mode) { ["base"] = baseNode, ["sub"] = subscript };
        return baseNode;
    }

    public ParseNode ParseFunction(string breakOnTokenText = null, string name = null)
    {
        var token = Fetch();
        var func = token.Text;
        if (!Registries.Functions.TryGetValue(func, out var funcData)) return null;
        Consume();

        if (name != null && name != "atom" && !funcData.AllowedInArgument)
            throw new ParseError("Got function '" + func + "' with no arguments" +
                                 (name != null ? " as " + name : ""), token);
        if (Mode == "text" && !funcData.AllowedInText)
            throw new ParseError("Can't use function '" + func + "' in text mode", token);
        if (Mode == "math" && funcData.AllowedInMath == false)
            throw new ParseError("Can't use function '" + func + "' in math mode", token);

        var (args, optArgs) = ParseArguments(func, funcData);
        return CallFunction(func, args, optArgs, token, breakOnTokenText);
    }

    public ParseNode CallFunction(string name, List<ParseNode> args, List<ParseNode> optArgs,
        Token token = null, string breakOnTokenText = null)
    {
        var context = new FunctionContext
        {
            FuncName = name,
            Parser = this,
            Token = token,
            BreakOnTokenText = breakOnTokenText,
        };
        if (Registries.Functions.TryGetValue(name, out var func) && func.Handler != null)
            return func.Handler(context, args, optArgs);
        throw new ParseError($"No function handler for {name}");
    }

    public (List<ParseNode> Args, List<ParseNode> OptArgs) ParseArguments(string func, FunctionSpec funcData)
        => ParseArguments(func, funcData.NumArgs, funcData.NumOptionalArgs, funcData.ArgTypes,
            funcData.Primitive, funcData.Type);

    public (List<ParseNode> Args, List<ParseNode> OptArgs) ParseArguments(string func, EnvSpec funcData)
        => ParseArguments(func, funcData.NumArgs, funcData.NumOptionalArgs, funcData.ArgTypes,
            false, funcData.Type);

    public (List<ParseNode> Args, List<ParseNode> OptArgs) ParseArguments(string func,
        int numArgs, int numOptionalArgs, List<string> argTypes, bool primitive, string type)
    {
        int totalArgs = numArgs + numOptionalArgs;
        if (totalArgs == 0) return (new List<ParseNode>(), new List<ParseNode>());

        var args = new List<ParseNode>();
        var optArgs = new List<ParseNode>();
        for (int i = 0; i < totalArgs; i++)
        {
            string argType = argTypes != null && i < argTypes.Count ? argTypes[i] : null;
            bool isOptional = i < numOptionalArgs;
            if ((primitive && argType == null) || (type == "sqrt" && i == 1 && optArgs.Count > 0 && optArgs[0] == null))
                argType = "primitive";

            var arg = ParseGroupOfType($"argument to '{func}'", argType, isOptional);
            if (isOptional)
            {
                optArgs.Add(arg);
            }
            else if (arg != null)
            {
                args.Add(arg);
            }
            else
            {
                throw new ParseError("Null argument, please report this as a bug");
            }
        }
        return (args, optArgs);
    }

    public ParseNode ParseGroupOfType(string name, string type, bool optional)
    {
        switch (type)
        {
            case "color": return ParseColorGroup(optional);
            case "size": return ParseSizeGroup(optional);
            case "url": return ParseUrlGroup(optional);
            case "math":
            case "text": return ParseArgumentGroup(optional, type);
            case "hbox":
            {
                var group = ParseArgumentGroup(optional, "text");
                return group != null ? new ParseNode("styling", group.Mode)
                {
                    ["body"] = new List<ParseNode> { group },
                    ["style"] = "text",
                    ["resetFont"] = true,
                } : null;
            }
            case "raw":
            {
                Gullet.ConsumeSpaces();
                var percentCatcode = Gullet.Lexer.Catcodes["%"];
                Gullet.Lexer.SetCatcode("%", 12);
                var token = ParseStringGroup(optional);
                Gullet.Lexer.SetCatcode("%", percentCatcode);
                return token != null ? new ParseNode("raw", "text") { ["string"] = token.Text } : null;
            }
            case "primitive":
            {
                if (optional) throw new ParseError("A primitive argument cannot be optional");
                var group = ParseGroup(name);
                if (group == null) throw new ParseError("Expected group as " + name, Fetch());
                return group;
            }
            case "original":
            case null:
                return ParseArgumentGroup(optional);
            default:
                throw new ParseError("Unknown group type as " + name, Fetch());
        }
    }

    public void ConsumeSpaces()
    {
        while (Fetch().Text == " ") Consume();
    }

    public Token ParseStringGroup(bool optional)
    {
        var argToken = Gullet.ScanArgument(optional);
        if (argToken == null) return null;
        string str = "";
        Token nextToken;
        while ((nextToken = Fetch()).Text != "EOF")
        {
            str += nextToken.Text;
            Consume();
        }
        Consume();
        argToken.Text = str;
        return argToken;
    }

    public Token ParseRegexGroup(Regex regex, string modeName)
    {
        var firstToken = Fetch();
        var lastToken = firstToken;
        string str = "";
        Token nextToken;
        while ((nextToken = Fetch()).Text != "EOF" && regex.IsMatch(str + nextToken.Text))
        {
            lastToken = nextToken;
            str += lastToken.Text;
            Consume();
        }
        if (str == "")
            throw new ParseError("Invalid " + modeName + ": '" + firstToken.Text + "'", firstToken);
        return firstToken.Range(lastToken, str);
    }

    private static readonly Regex ColorRegex = new(
        "^(#[a-f0-9]{3,4}|#[a-f0-9]{6}|#[a-f0-9]{8}|[a-f0-9]{6}|[a-z]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HexColorRegex = new("^[0-9a-f]{6}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ParseNode ParseColorGroup(bool optional)
    {
        var res = ParseStringGroup(optional);
        if (res == null) return null;
        var match = ColorRegex.Match(res.Text);
        if (!match.Success) throw new ParseError("Invalid color: '" + res.Text + "'", res);
        var color = match.Value;
        if (HexColorRegex.IsMatch(color)) color = "#" + color;
        return new ParseNode("color-token", Mode) { ["color"] = color };
    }

    private static readonly Regex SizeRegex = new(
        "^[-+]? *(?:$|\\d+|\\d+\\.\\d*|\\.\\d*) *[a-z]{0,2} *$", RegexOptions.Compiled);
    private static readonly Regex SizeParseRegex = new(
        "([-+]?) *(\\d+(?:\\.\\d*)?|\\.\\d+) *([a-z]{2})", RegexOptions.Compiled);

    public ParseNode ParseSizeGroup(bool optional)
    {
        Token res;
        bool isBlank = false;
        Gullet.ConsumeSpaces();
        if (!optional && Gullet.Future().Text != "{")
            res = ParseRegexGroup(SizeRegex, "size");
        else
            res = ParseStringGroup(optional);
        if (res == null) return null;
        if (!optional && res.Text.Length == 0)
        {
            res.Text = "0pt";
            isBlank = true;
        }
        var match = SizeParseRegex.Match(res.Text);
        if (!match.Success) throw new ParseError("Invalid size: '" + res.Text + "'", res);
        var data = new Measurement(
            double.Parse(match.Groups[1].Value + match.Groups[2].Value,
                System.Globalization.CultureInfo.InvariantCulture),
            match.Groups[3].Value);
        if (!Units.ValidUnit(data)) throw new ParseError("Invalid unit: '" + data.Unit + "'", res);
        return new ParseNode("size", Mode) { ["value"] = data, ["isBlank"] = isBlank };
    }

    private static readonly Regex UrlEscapeRegex = new("\\\\([#$%&~_^{}])", RegexOptions.Compiled);

    public ParseNode ParseUrlGroup(bool optional)
    {
        var percentCatcode = Gullet.Lexer.Catcodes["%"];
        var tildeCatcode = Gullet.Lexer.Catcodes["~"];
        Gullet.Lexer.SetCatcode("%", 13);
        Gullet.Lexer.SetCatcode("~", 12);
        var res = ParseStringGroup(optional);
        Gullet.Lexer.SetCatcode("%", percentCatcode);
        Gullet.Lexer.SetCatcode("~", tildeCatcode);
        if (res == null) return null;
        var url = UrlEscapeRegex.Replace(res.Text, "$1");
        return new ParseNode("url", Mode) { ["url"] = url };
    }

    public ParseNode ParseArgumentGroup(bool optional, string mode = null)
    {
        var argToken = Gullet.ScanArgument(optional);
        if (argToken == null) return null;
        var outerMode = Mode;
        if (mode != null) SwitchMode(mode);
        Gullet.BeginGroup();
        var expression = ParseExpression(false, "EOF");
        Expect("EOF");
        Gullet.EndGroup();
        var result = new ParseNode("ordgroup", Mode)
        {
            ["loc"] = argToken.Loc,
            ["body"] = expression,
        };
        if (mode != null) SwitchMode(outerMode);
        return result;
    }

    public ParseNode ParseGroup(string name, string breakOnTokenText = null)
    {
        var firstToken = Fetch();
        var text = firstToken.Text;
        ParseNode result;
        if (text == "{" || text == "\\begingroup")
        {
            Consume();
            var groupEnd = text == "{" ? "}" : "\\endgroup";
            Gullet.BeginGroup();
            var expression = ParseExpression(false, groupEnd);
            var lastToken = Fetch();
            Expect(groupEnd);
            Gullet.EndGroup();
            result = new ParseNode("ordgroup", Mode)
            {
                ["loc"] = SourceLocation.Range(firstToken, lastToken),
                ["body"] = expression,
            };
            if (text == "\\begingroup") result["semisimple"] = true;
        }
        else
        {
            result = ParseFunction(breakOnTokenText, name) ?? ParseSymbol();
            if (result == null && text.Length > 0 && text[0] == '\\' &&
                !MacroExpander.ImplicitCommands.ContainsKey(text))
            {
                if (Settings.ThrowOnError)
                    throw new ParseError("Undefined control sequence: " + text, firstToken);
                result = FormatUnsupportedCmd(text);
                Consume();
            }
        }
        return result;
    }

    public void FormLigatures(List<ParseNode> group)
    {
        int n = group.Count - 1;
        for (int i = 0; i < n; ++i)
        {
            var a = group[i];
            if (a.Type != "textord") continue;
            var v = a.Str("text");
            if (i + 1 >= group.Count) continue;
            var next = group[i + 1];
            if (next == null || next.Type != "textord") continue;
            if (v == "-" && next.Str("text") == "-")
            {
                var afterNext = i + 2 < group.Count ? group[i + 2] : null;
                if (i + 1 < n && afterNext != null && afterNext.Type == "textord" &&
                    afterNext.Str("text") == "-")
                {
                    group.RemoveRange(i, 3);
                    group.Insert(i, new ParseNode("textord", "text")
                    {
                        ["loc"] = SourceLocation.Range(a, afterNext),
                        ["text"] = "---",
                    });
                    n -= 2;
                }
                else
                {
                    group.RemoveRange(i, 2);
                    group.Insert(i, new ParseNode("textord", "text")
                    {
                        ["loc"] = SourceLocation.Range(a, next),
                        ["text"] = "--",
                    });
                    n -= 1;
                }
            }
            if ((v == "'" || v == "`") && next.Str("text") == v)
            {
                group.RemoveRange(i, 2);
                group.Insert(i, new ParseNode("textord", "text")
                {
                    ["loc"] = SourceLocation.Range(a, next),
                    ["text"] = v + v,
                });
                n -= 1;
            }
        }
    }

    private static readonly Regex VerbRegex = new("^\\\\verb[^a-zA-Z]", RegexOptions.Compiled);

    public ParseNode ParseSymbol()
    {
        var nucleus = Fetch();
        var text = nucleus.Text;
        if (VerbRegex.IsMatch(text))
        {
            Consume();
            var arg = text.Substring(5);
            bool star = arg.Length > 0 && arg[0] == '*';
            if (star) arg = arg.Substring(1);
            if (arg.Length < 2 || arg[0] != arg[^1])
                throw new ParseError("\\verb assertion failed --\n                    please report what input caused this bug");
            arg = arg.Substring(1, arg.Length - 2);
            return new ParseNode("verb", "text") { ["body"] = arg, ["star"] = star };
        }

        if (text.Length > 0 && UnicodeData.Symbols.ContainsKey(text[0].ToString()) &&
            Symbols.Get(Mode, text[0].ToString()) == null)
        {
            if (StrictHelper.IsTrue(Settings.Strict) && Mode == "math")
                StrictHelper.Handle(Settings.Strict, "unicodeTextInMathMode",
                    $"Accented Unicode text character \"{text[0]}\" used in math mode", nucleus, true);
            text = UnicodeData.Symbols[text[0].ToString()] + text.Substring(1);
        }

        var match = Lexer.CombiningDiacriticalMarksEndRegex.Match(text);
        if (match.Success)
        {
            text = text.Substring(0, match.Index);
            if (text == "i") text = "\u0131";
            else if (text == "j") text = "\u0237";
        }

        ParseNode symbol;
        var info = Symbols.Get(Mode, text);
        if (info != null)
        {
            if (StrictHelper.IsTrue(Settings.Strict) && Mode == "math" &&
                Symbols.ExtraLatin.Contains(text[0]))
                StrictHelper.Handle(Settings.Strict, "unicodeTextInMathMode",
                    $"Latin-1/Unicode text character \"{text[0]}\" used in math mode", nucleus, true);
            var group = info.Group;
            var loc = SourceLocation.Range(nucleus);
            if (IsAtom(group))
                symbol = new ParseNode("atom", Mode)
                {
                    ["family"] = group, ["loc"] = loc, ["text"] = text,
                };
            else
                symbol = new ParseNode(group, Mode) { ["loc"] = loc, ["text"] = text };
        }
        else if (text.Length > 0 && text[0] >= 0x80)
        {
            if (StrictHelper.IsTrue(Settings.Strict))
            {
                if (!UnicodeScripts.SupportedCodepoint(text[0]))
                    StrictHelper.Handle(Settings.Strict, "unknownSymbol",
                        $"Unrecognized Unicode character \"{text[0]}\" ({(int)text[0]})", nucleus, true);
                else if (Mode == "math")
                    StrictHelper.Handle(Settings.Strict, "unicodeTextInMathMode",
                        $"Unicode text character \"{text[0]}\" used in math mode", nucleus, true);
            }
            symbol = new ParseNode("textord", "text")
            {
                ["loc"] = SourceLocation.Range(nucleus),
                ["text"] = text,
            };
        }
        else
        {
            return null;
        }
        Consume();

        if (match.Success)
        {
            foreach (var accentChar in match.Value)
            {
                var accent = accentChar.ToString();
                if (!UnicodeData.Accents.TryGetValue(accent, out var accentInfo))
                    throw new ParseError($"Unknown accent ' {accent}'", nucleus);
                var command = (Mode == "math" ? accentInfo.Math : null) ?? accentInfo.Text;
                if (string.IsNullOrEmpty(command))
                    throw new ParseError($"Accent {accent} unsupported in {Mode} mode", nucleus);
                symbol = new ParseNode("accent", Mode)
                {
                    ["loc"] = SourceLocation.Range(nucleus),
                    ["label"] = command,
                    ["isStretchy"] = false,
                    ["isShifty"] = true,
                    ["base"] = symbol,
                };
            }
        }
        return symbol;
    }

    public static bool IsAtom(string group) => group is "bin" or "close" or "inner" or "open" or "punct" or "rel";
}

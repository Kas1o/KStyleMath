using System.Globalization;
using System.Text.RegularExpressions;

namespace KStyleMath;

public static class DefFunctions
{
    private static readonly Dictionary<string, string> GlobalMap = new(StringComparer.Ordinal)
    {
        ["\\global"] = "\\global",
        ["\\long"] = "\\\\globallong",
        ["\\\\globallong"] = "\\\\globallong",
        ["\\def"] = "\\gdef",
        ["\\gdef"] = "\\gdef",
        ["\\edef"] = "\\xdef",
        ["\\xdef"] = "\\xdef",
        ["\\let"] = "\\\\globallet",
        ["\\futurelet"] = "\\\\globalfuture",
    };

    private static readonly Regex InvalidControlSequence = new(
        @"^(?:[\\{}$&#^_]|EOF)$", RegexOptions.Compiled);

    private static readonly Regex ArgumentNumber = new("^[1-9]$", RegexOptions.Compiled);

    private static string CheckControlSequence(Token tok)
    {
        var name = tok.Text;
        if (InvalidControlSequence.IsMatch(name))
        {
            throw new ParseError("Expected a control sequence", tok);
        }
        return name;
    }

    private static Token GetRHS(Parser parser)
    {
        var tok = parser.Gullet.PopToken();
        if (tok.Text == "=")
        {
            // consume optional equals
            tok = parser.Gullet.PopToken();
            if (tok.Text == " ")
            {
                // consume one optional space
                tok = parser.Gullet.PopToken();
            }
        }
        return tok;
    }

    private static void LetCommand(Parser parser, string name, Token tok, bool global)
    {
        var macro = parser.Gullet.Macros.Get(tok.Text);
        if (macro == null)
        {
            // don't expand it later even if a macro with the same name is defined
            // e.g., \let\foo=\frac \def\frac{\relax} \frac12
            tok.Noexpand = true;
            macro = new MacroExpansion
            {
                Tokens = new List<Token> { tok },
                NumArgs = 0,
                // reproduce the same behavior in expansion
                Unexpandable = !parser.Gullet.IsExpandable(tok.Text),
            };
        }
        parser.Gullet.Macros.Set(name, macro, global);
    }

    public static void Define()
    {
        // <assignment> -> <non-macro assignment>|<macro assignment>
        // <non-macro assignment> -> <simple assignment>|\global<non-macro assignment>
        // <macro assignment> -> <definition>|<prefix><macro assignment>
        // <prefix> -> \global|\long|\outer
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "internal",
            Names = new List<string>
            {
                "\\global", "\\long",
                "\\\\globallong", // can’t be entered directly
            },
            NumArgs = 0,
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                parser.ConsumeSpaces();
                var token = parser.Fetch();
                if (GlobalMap.TryGetValue(token.Text, out var replacement) && replacement != null)
                {
                    // KaTeX doesn't have \par, so ignore \long
                    if (funcName == "\\global" || funcName == "\\\\globallong")
                    {
                        token.Text = GlobalMap[token.Text];
                    }
                    return ParseNodeUtil.AssertNodeType(parser.ParseFunction(), "internal");
                }
                throw new ParseError($"Invalid token after macro prefix", token);
            },
        });

        // Basic support for macro definitions: \def, \gdef, \edef, \xdef
        // <definition> -> <def><control sequence><definition text>
        // <def> -> \def|\gdef|\edef|\xdef
        // <definition text> -> <parameter text><left brace><balanced text><right brace>
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "internal",
            Names = new List<string> { "\\def", "\\gdef", "\\edef", "\\xdef" },
            NumArgs = 0,
            AllowedInText = true,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var tok = parser.Gullet.PopToken();
                var name = tok.Text;
                if (InvalidControlSequence.IsMatch(name))
                {
                    throw new ParseError("Expected a control sequence", tok);
                }

                int numArgs = 0;
                Token insert = null;
                var delimiters = new List<List<string>> { new List<string>() };
                // <parameter text> contains no braces
                while (parser.Gullet.Future().Text != "{")
                {
                    tok = parser.Gullet.PopToken();
                    if (tok.Text == "#")
                    {
                        // If the very last character of the <parameter text> is #, so that
                        // this # is immediately followed by {, TeX will behave as if the {
                        // had been inserted at the right end of both the parameter text
                        // and the replacement text.
                        if (parser.Gullet.Future().Text == "{")
                        {
                            insert = parser.Gullet.Future();
                            delimiters[numArgs].Add("{");
                            break;
                        }

                        // A parameter, the first appearance of # must be followed by 1,
                        // the next by 2, and so on; up to nine #’s are allowed
                        tok = parser.Gullet.PopToken();
                        if (!ArgumentNumber.IsMatch(tok.Text))
                        {
                            throw new ParseError($"Invalid argument number \"{tok.Text}\"");
                        }
                        if (int.Parse(tok.Text, CultureInfo.InvariantCulture) != numArgs + 1)
                        {
                            throw new ParseError(
                                $"Argument number \"{tok.Text}\" out of order");
                        }
                        numArgs++;
                        delimiters.Add(new List<string>());
                    }
                    else if (tok.Text == "EOF")
                    {
                        throw new ParseError("Expected a macro definition");
                    }
                    else
                    {
                        delimiters[numArgs].Add(tok.Text);
                    }
                }
                // replacement text, enclosed in '{' and '}' and properly nested
                var tokens = parser.Gullet.ConsumeArg().Tokens;
                if (insert != null)
                {
                    tokens.Insert(0, insert);
                }

                if (funcName == "\\edef" || funcName == "\\xdef")
                {
                    tokens = parser.Gullet.ExpandTokens(tokens);
                    tokens.Reverse(); // to fit in with stack order
                }
                // Final arg is the expansion of the macro
                parser.Gullet.Macros.Set(name, new MacroExpansion
                {
                    Tokens = tokens,
                    NumArgs = numArgs,
                    Delimiters = delimiters,
                }, funcName == GlobalMap[funcName]);

                return new ParseNode("internal", parser.Mode);
            },
        });

        // <simple assignment> -> <let assignment>
        // <let assignment> -> \futurelet<control sequence><token><token>
        //     | \let<control sequence><equals><one optional space><token>
        // <equals> -> <optional spaces>|<optional spaces>=
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "internal",
            Names = new List<string>
            {
                "\\let",
                "\\\\globallet", // can’t be entered directly
            },
            NumArgs = 0,
            AllowedInText = true,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var name = CheckControlSequence(parser.Gullet.PopToken());
                parser.Gullet.ConsumeSpaces();
                var tok = GetRHS(parser);
                LetCommand(parser, name, tok, funcName == "\\\\globallet");
                return new ParseNode("internal", parser.Mode);
            },
        });

        // ref: https://www.tug.org/TUGboat/tb09-3/tb22bechtolsheim.pdf
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "internal",
            Names = new List<string>
            {
                "\\futurelet",
                "\\\\globalfuture", // can’t be entered directly
            },
            NumArgs = 0,
            AllowedInText = true,
            Primitive = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var name = CheckControlSequence(parser.Gullet.PopToken());
                var middle = parser.Gullet.PopToken();
                var tok = parser.Gullet.PopToken();
                LetCommand(parser, name, tok, funcName == "\\\\globalfuture");
                parser.Gullet.PushToken(tok);
                parser.Gullet.PushToken(middle);
                return new ParseNode("internal", parser.Mode);
            },
        });
    }
}

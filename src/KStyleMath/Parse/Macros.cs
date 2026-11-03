using System.Text.RegularExpressions;

namespace KStyleMath;

/// <summary>
/// Predefined macros for KaTeX.
/// This can be used to define some commands in terms of others.
/// Port of .katex-ref/src/defineMacro.ts (the global macro map + defineMacro).
/// </summary>
public static class MacrosData
{
    /// <summary>All registered global/built-in macros.</summary>
    public static readonly Dictionary<string, object> Builtins = new(StringComparer.Ordinal);

    static MacrosData()
    {
        MacroDefinitions.Define();
    }

    /// <summary>Port of defineMacro(name, body) from defineMacro.ts.</summary>
    public static void DefineMacro(string name, object body) => Builtins[name] = body;
}

/// <summary>Port of .katex-ref/src/macros.ts.</summary>
public static class MacroDefinitions
{
    // Lookup table for parsing numbers in base 8 through 16
    private static readonly Dictionary<string, int> DigitToNumber = new(StringComparer.Ordinal)
    {
        ["0"] = 0, ["1"] = 1, ["2"] = 2, ["3"] = 3, ["4"] = 4, ["5"] = 5, ["6"] = 6, ["7"] = 7, ["8"] = 8,
        ["9"] = 9, ["a"] = 10, ["A"] = 10, ["b"] = 11, ["B"] = 11, ["c"] = 12, ["C"] = 12,
        ["d"] = 13, ["D"] = 13, ["e"] = 14, ["E"] = 14, ["f"] = 15, ["F"] = 15,
    };

    private static readonly Regex ValidNumberOfArguments = new("^\\s*[0-9]+\\s*$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> DotsByToken = new(StringComparer.Ordinal)
    {
        [","] = "\\dotsc",
        ["\\not"] = "\\dotsb",
        // \keybin@ checks for the following:
        ["+"] = "\\dotsb",
        ["="] = "\\dotsb",
        ["<"] = "\\dotsb",
        [">"] = "\\dotsb",
        ["-"] = "\\dotsb",
        ["*"] = "\\dotsb",
        [":"] = "\\dotsb",
        // Symbols whose definition starts with \DOTSB:
        ["\\DOTSB"] = "\\dotsb",
        ["\\coprod"] = "\\dotsb",
        ["\\bigvee"] = "\\dotsb",
        ["\\bigwedge"] = "\\dotsb",
        ["\\biguplus"] = "\\dotsb",
        ["\\bigcap"] = "\\dotsb",
        ["\\bigcup"] = "\\dotsb",
        ["\\prod"] = "\\dotsb",
        ["\\sum"] = "\\dotsb",
        ["\\bigotimes"] = "\\dotsb",
        ["\\bigoplus"] = "\\dotsb",
        ["\\bigodot"] = "\\dotsb",
        ["\\bigsqcup"] = "\\dotsb",
        ["\\And"] = "\\dotsb",
        ["\\longrightarrow"] = "\\dotsb",
        ["\\Longrightarrow"] = "\\dotsb",
        ["\\longleftarrow"] = "\\dotsb",
        ["\\Longleftarrow"] = "\\dotsb",
        ["\\longleftrightarrow"] = "\\dotsb",
        ["\\Longleftrightarrow"] = "\\dotsb",
        ["\\mapsto"] = "\\dotsb",
        ["\\longmapsto"] = "\\dotsb",
        ["\\hookrightarrow"] = "\\dotsb",
        ["\\doteq"] = "\\dotsb",
        // Symbols whose definition starts with \mathbin:
        ["\\mathbin"] = "\\dotsb",
        // Symbols whose definition starts with \mathrel:
        ["\\mathrel"] = "\\dotsb",
        ["\\relbar"] = "\\dotsb",
        ["\\Relbar"] = "\\dotsb",
        ["\\xrightarrow"] = "\\dotsb",
        ["\\xleftarrow"] = "\\dotsb",
        // Symbols whose definition starts with \DOTSI:
        ["\\DOTSI"] = "\\dotsi",
        ["\\int"] = "\\dotsi",
        ["\\oint"] = "\\dotsi",
        ["\\iint"] = "\\dotsi",
        ["\\iiint"] = "\\dotsi",
        ["\\iiiint"] = "\\dotsi",
        ["\\idotsint"] = "\\dotsi",
        // Symbols whose definition starts with \DOTSX:
        ["\\DOTSX"] = "\\dotsx",
    };

    private static readonly HashSet<string> DotsbGroups = new(StringComparer.Ordinal) { "bin", "rel" };

    private static readonly Dictionary<string, bool> SpaceAfterDots = new(StringComparer.Ordinal)
    {
        // \rightdelim@ checks for the following:
        [")"] = true,
        ["]"] = true,
        ["\\rbrack"] = true,
        ["\\}"] = true,
        ["\\rbrace"] = true,
        ["\\rangle"] = true,
        ["\\rceil"] = true,
        ["\\rfloor"] = true,
        ["\\rgroup"] = true,
        ["\\rmoustache"] = true,
        ["\\right"] = true,
        ["\\bigr"] = true,
        ["\\biggr"] = true,
        ["\\Bigr"] = true,
        ["\\Biggr"] = true,
        // \extra@ also tests for the following:
        ["$"] = true,
        // \extrap@ checks for the following:
        [";"] = true,
        ["."] = true,
        [","] = true,
    };

    private static void Def(string name, object body) => MacrosData.DefineMacro(name, body);

    private static MacroExpansion Expansion(List<Token> tokens, int numArgs)
        => new() { Tokens = tokens, NumArgs = numArgs };

    public static void Define()
    {
        //////////////////////////////////////////////////////////////////////
        // macro tools

        Def("\\noexpand", (MacroFunction)(context =>
        {
            // The expansion is the token itself; but that token is interpreted
            // as if its meaning were '\\relax' if it is a control sequence that
            // would ordinarily be expanded by TeX's expansion rules.
            var t = context.PopToken();
            if (context.IsExpandable(t.Text))
            {
                t.Noexpand = true;
                t.TreatAsRelax = true;
            }
            return Expansion(new List<Token> { t }, 0);
        }));

        Def("\\expandafter", (MacroFunction)(context =>
        {
            // TeX first reads the token that comes immediately after \expandafter,
            // without expanding it; let's call this token t. Then TeX reads the
            // token that comes after t (and possibly more tokens, if that token
            // has an argument), replacing it by its expansion. Finally TeX puts
            // t back in front of that expansion.
            var t = context.PopToken();
            context.ExpandOnce(true); // expand only an expandable token
            return Expansion(new List<Token> { t }, 0);
        }));

        // LaTeX's \@firstoftwo{#1}{#2} expands to #1, skipping #2
        // TeX source: \long\def\@firstoftwo#1#2{#1}
        Def("\\@firstoftwo", (MacroFunction)(context =>
        {
            var args = context.ConsumeArgs(2);
            return Expansion(args[0], 0);
        }));

        // LaTeX's \@secondoftwo{#1}{#2} expands to #2, skipping #1
        // TeX source: \long\def\@secondoftwo#1#2{#2}
        Def("\\@secondoftwo", (MacroFunction)(context =>
        {
            var args = context.ConsumeArgs(2);
            return Expansion(args[1], 0);
        }));

        // LaTeX's \@ifnextchar{#1}{#2}{#3} looks ahead to the next (unexpanded)
        // symbol that isn't a space, consuming any spaces but not consuming the
        // first nonspace character.  If that nonspace character matches #1, then
        // the macro expands to #2; otherwise, it expands to #3.
        Def("\\@ifnextchar", (MacroFunction)(context =>
        {
            var args = context.ConsumeArgs(3);  // symbol, if, else
            context.ConsumeSpaces();
            var nextToken = context.Future();
            if (args[0].Count == 1 && args[0][0].Text == nextToken.Text)
            {
                return Expansion(args[1], 0);
            }
            else
            {
                return Expansion(args[2], 0);
            }
        }));

        // LaTeX's \@ifstar{#1}{#2} looks ahead to the next (unexpanded) symbol.
        // If it is `*`, then it consumes the symbol, and the macro expands to #1;
        // otherwise, the macro expands to #2 (without consuming the symbol).
        // TeX source: \def\@ifstar#1{\@ifnextchar *{\@firstoftwo{#1}}}
        Def("\\@ifstar", "\\@ifnextchar *{\\@firstoftwo{#1}}");

        // LaTeX's \TextOrMath{#1}{#2} expands to #1 in text mode, #2 in math mode
        Def("\\TextOrMath", (MacroFunction)(context =>
        {
            var args = context.ConsumeArgs(2);
            if (context.Mode == "text")
            {
                return Expansion(args[0], 0);
            }
            else
            {
                return Expansion(args[1], 0);
            }
        }));

        // TeX \char makes a literal character (catcode 12) using the following forms:
        // (see The TeXBook, p. 43)
        //   \char123  -- decimal
        //   \char'123 -- octal
        //   \char"123 -- hex
        //   \char`x   -- character that can be written (i.e. isn't active)
        //   \char`\x  -- character that cannot be written (e.g. %)
        // These all refer to characters from the font, so we turn them into special
        // calls to a function \@char dealt with in the Parser.
        Def("\\char", (MacroFunction)(context =>
        {
            var token = context.PopToken();
            int? baseNum = null;
            int number = 0;
            if (token.Text == "'")
            {
                baseNum = 8;
                token = context.PopToken();
            }
            else if (token.Text == "\"")
            {
                baseNum = 16;
                token = context.PopToken();
            }
            else if (token.Text == "`")
            {
                token = context.PopToken();
                if (token.Text[0] == '\\')
                {
                    number = token.Text.Length > 1 ? token.Text[1] : 0;
                }
                else if (token.Text == "EOF")
                {
                    throw new ParseError("\\char` missing argument");
                }
                else
                {
                    number = token.Text[0];
                }
            }
            else
            {
                baseNum = 10;
            }
            if (baseNum.HasValue)
            {
                var baseValue = baseNum.Value;
                // Parse a number in the given base, starting with first `token`.
                if (!DigitToNumber.TryGetValue(token.Text, out number) || number >= baseValue)
                {
                    throw new ParseError($"Invalid base-{baseValue} digit {token.Text}");
                }
                int digit;
                while (DigitToNumber.TryGetValue(context.Future().Text, out digit) && digit < baseValue)
                {
                    number *= baseValue;
                    number += digit;
                    context.PopToken();
                }
            }
            return $"\\@char{{{number}}}";
        }));

        // \newcommand{\macro}[args]{definition}
        // \renewcommand{\macro}[args]{definition}
        // TODO: Optional arguments: \newcommand{\macro}[args][default]{definition}
        Def("\\newcommand",
            (MacroFunction)(context => Newcommand(context, false, true, false)));
        Def("\\renewcommand",
            (MacroFunction)(context => Newcommand(context, true, false, false)));
        Def("\\providecommand",
            (MacroFunction)(context => Newcommand(context, true, true, true)));

        // terminal (console) tools
        Def("\\message", (MacroFunction)(context =>
        {
            var arg = context.ConsumeArgs(1)[0];
            arg.Reverse();
            Console.WriteLine(string.Concat(arg.Select(token => token.Text)));
            return "";
        }));
        Def("\\errmessage", (MacroFunction)(context =>
        {
            var arg = context.ConsumeArgs(1)[0];
            arg.Reverse();
            Console.Error.WriteLine(string.Concat(arg.Select(token => token.Text)));
            return "";
        }));
        Def("\\show", (MacroFunction)(context =>
        {
            var tok = context.PopToken();
            var name = tok.Text;
            Console.WriteLine(tok.Text + " " + FormatMacro(context.Macros.Get(name)) + " " +
                (Registries.Functions.TryGetValue(name, out var fn) ? fn.Type : "undefined") + " " +
                FormatSymbol(Symbols.Get("math", name)) + " " + FormatSymbol(Symbols.Get("text", name)));
            return "";
        }));

        //////////////////////////////////////////////////////////////////////
        // Grouping
        // \let\bgroup={ \let\egroup=}
        Def("\\bgroup", "{");
        Def("\\egroup", "}");

        // Symbols from latex.ltx:
        // \def~{\nobreakspace{}}
        // \def\lq{`}
        // \def\rq{'}
        // \def \aa {\r a}
        // \def \AA {\r A}
        Def("~", "\\nobreakspace");
        Def("\\lq", "`");
        Def("\\rq", "'");
        Def("\\aa", "\\r a");
        Def("\\AA", "\\r A");

        // Copyright (C) and registered (R) symbols. Use raw symbol in MathML.
        // \DeclareTextCommandDefault{\textcopyright}{\textcircled{c}}
        // \DeclareTextCommandDefault{\textregistered}{\textcircled{%
        //      \check@mathfonts\fontsize\sf@size\z@\math@fontsfalse\selectfont R}}
        // \DeclareRobustCommand{\copyright}{%
        //    \ifmmode{\nfss@text{\textcopyright}}\else\textcopyright\fi}
        Def("\\textcopyright", "\\html@mathml{\\textcircled{c}}{\\char`\u00A9}");
        Def("\\copyright",
            "\\TextOrMath{\\textcopyright}{\\text{\\textcopyright}}");
        Def("\\textregistered",
            "\\html@mathml{\\textcircled{\\scriptsize R}}{\\char`\u00AE}");

        // Characters omitted from Unicode range 1D400-1D7FF
        Def("\u212C", "\\mathscr{B}");  // script
        Def("\u2130", "\\mathscr{E}");
        Def("\u2131", "\\mathscr{F}");
        Def("\u210B", "\\mathscr{H}");
        Def("\u2110", "\\mathscr{I}");
        Def("\u2112", "\\mathscr{L}");
        Def("\u2133", "\\mathscr{M}");
        Def("\u211B", "\\mathscr{R}");
        Def("\u212D", "\\mathfrak{C}");  // Fraktur
        Def("\u210C", "\\mathfrak{H}");
        Def("\u2128", "\\mathfrak{Z}");

        // Define \Bbbk with a macro that works in both HTML and MathML.
        Def("\\Bbbk", "\\Bbb{k}");

        // \llap and \rlap render their contents in text mode
        Def("\\llap", "\\mathllap{\\textrm{#1}}");
        Def("\\rlap", "\\mathrlap{\\textrm{#1}}");
        Def("\\clap", "\\mathclap{\\textrm{#1}}");

        // \mathstrut from the TeXbook, p 360
        Def("\\mathstrut", "\\vphantom{(}");

        // \underbar from TeXbook p 353
        Def("\\underbar", "\\underline{\\text{#1}}");

        // \not is defined by base/fontmath.ltx via
        // \DeclareMathSymbol{\not}{\mathrel}{symbols}{"36}
        // It's thus treated like a \mathrel, but defined by a symbol that has zero
        // width but extends to the right.  We use \rlap to get that spacing.
        // For MathML we write U+0338 here. buildMathML.js will then do the overlay.
        Def("\\not", "\\html@mathml{\\mathrel{\\mathrlap\\@not}\\nobreak}" +
            "{\\char\"338}");

        // Negated symbols from base/fontmath.ltx:
        // \def\neq{\not=} \let\ne=\neq
        // \DeclareRobustCommand
        //   \notin{\mathrel{\m@th\mathpalette\c@ncel\in}}
        // \def\c@ncel#1#2{\m@th\ooalign{$\hfil#1\mkern1mu/\hfil$\crcr$#1#2$}}
        Def("\\neq", "\\html@mathml{\\mathrel{\\not=}}{\\mathrel{\\char`\u2260}}");
        Def("\\ne", "\\neq");
        Def("\u2260", "\\neq");
        Def("\\notin", "\\html@mathml{\\mathrel{{\\in}\\mathllap{/\\mskip1mu}}}"
                       + "{\\mathrel{\\char`\u2209}}");
        Def("\u2209", "\\notin");

        // Unicode stacked relations
        Def("\u2258", "\\html@mathml{" +
            "\\mathrel{=\\kern{-1em}\\raisebox{0.4em}{$\\scriptsize\\frown$}}" +
            "}{\\mathrel{\\char`\u2258}}");
        Def("\u2259",
            "\\html@mathml{\\stackrel{\\tiny\\wedge}{=}}{\\mathrel{\\char`\u2258}}");
        Def("\u225A",
            "\\html@mathml{\\stackrel{\\tiny\\vee}{=}}{\\mathrel{\\char`\u225A}}");
        Def("\u225B",
            "\\html@mathml{\\stackrel{\\scriptsize\\star}{=}}" +
            "{\\mathrel{\\char`\u225B}}");
        Def("\u225D",
            "\\html@mathml{\\stackrel{\\tiny\\mathrm{def}}{=}}" +
            "{\\mathrel{\\char`\u225D}}");
        Def("\u225E",
            "\\html@mathml{\\stackrel{\\tiny\\mathrm{m}}{=}}" +
            "{\\mathrel{\\char`\u225E}}");
        Def("\u225F",
            "\\html@mathml{\\stackrel{\\tiny?}{=}}{\\mathrel{\\char`\u225F}}");

        // Misc Unicode
        Def("\u27C2", "\\perp");
        Def("\u203C", "\\mathclose{!\\mkern-0.8mu!}");
        Def("\u220C", "\\notni");
        Def("\u231C", "\\ulcorner");
        Def("\u231D", "\\urcorner");
        Def("\u231E", "\\llcorner");
        Def("\u231F", "\\lrcorner");
        Def("\u00A9", "\\copyright");
        Def("\u00AE", "\\textregistered");

        // The KaTeX fonts have corners at codepoints that don't match Unicode.
        // For MathML purposes, use the Unicode code point.
        Def("\\ulcorner", "\\html@mathml{\\@ulcorner}{\\mathop{\\char\"231c}}");
        Def("\\urcorner", "\\html@mathml{\\@urcorner}{\\mathop{\\char\"231d}}");
        Def("\\llcorner", "\\html@mathml{\\@llcorner}{\\mathop{\\char\"231e}}");
        Def("\\lrcorner", "\\html@mathml{\\@lrcorner}{\\mathop{\\char\"231f}}");

        //////////////////////////////////////////////////////////////////////
        // LaTeX_2e

        // \vdots{\vbox{\baselineskip4\p@  \lineskiplimit\z@
        // \kern6\p@\hbox{.}\hbox{.}\hbox{.}}}
        // We'll call \varvdots, which gets a glyph from symbols.js.
        // The zero-width rule gets us an equivalent to the vertical 6pt kern.
        Def("\\vdots", "{\\varvdots\\rule{0pt}{15pt}}");
        Def("\u22ee", "\\vdots");

        //////////////////////////////////////////////////////////////////////
        // amsmath.sty
        // http://mirrors.concertpass.com/tex-archive/macros/latex/required/amsmath/amsmath.pdf

        // Italic Greek capital letters.  AMS defines these with \DeclareMathSymbol,
        // but they are equivalent to \mathit{\Letter}.
        Def("\\varGamma", "\\mathit{\\Gamma}");
        Def("\\varDelta", "\\mathit{\\Delta}");
        Def("\\varTheta", "\\mathit{\\Theta}");
        Def("\\varLambda", "\\mathit{\\Lambda}");
        Def("\\varXi", "\\mathit{\\Xi}");
        Def("\\varPi", "\\mathit{\\Pi}");
        Def("\\varSigma", "\\mathit{\\Sigma}");
        Def("\\varUpsilon", "\\mathit{\\Upsilon}");
        Def("\\varPhi", "\\mathit{\\Phi}");
        Def("\\varPsi", "\\mathit{\\Psi}");
        Def("\\varOmega", "\\mathit{\\Omega}");

        //\newcommand{\substack}[1]{\subarray{c}#1\endsubarray}
        Def("\\substack", "\\begin{subarray}{c}#1\\end{subarray}");

        // \renewcommand{\colon}{\nobreak\mskip2mu\mathpunct{}\nonscript
        // \mkern-\thinmuskip{:}\mskip6muplus1mu\relax}
        Def("\\colon", "\\nobreak\\mskip2mu\\mathpunct{}" +
            "\\mathchoice{\\mkern-3mu}{\\mkern-3mu}{}{}{:}\\mskip6mu\\relax");

        // \newcommand{\boxed}[1]{\fbox{\m@th$\displaystyle#1$}}
        Def("\\boxed", "\\fbox{$\\displaystyle{#1}$}");

        // \def\iff{\DOTSB\;\Longleftrightarrow\;}
        // \def\implies{\DOTSB\;\Longrightarrow\;}
        // \def\impliedby{\DOTSB\;\Longleftarrow\;}
        Def("\\iff", "\\DOTSB\\;\\Longleftrightarrow\\;");
        Def("\\implies", "\\DOTSB\\;\\Longrightarrow\\;");
        Def("\\impliedby", "\\DOTSB\\;\\Longleftarrow\\;");

        // \def\dddot#1{{\mathop{#1}\limits^{\vbox to-1.4\ex@{\kern-\tw@\ex@
        //  \hbox{\normalfont ...}\vss}}}}
        // We use \overset which avoids the vertical shift of \mathop.
        Def("\\dddot", "{\\overset{\\raisebox{-0.1ex}{\\normalsize ...}}{#1}}");
        Def("\\ddddot", "{\\overset{\\raisebox{-0.1ex}{\\normalsize ....}}{#1}}");

        // AMSMath's automatic \dots, based on \mdots@@ macro.

        Def("\\dots", (MacroFunction)(context =>
        {
            // TODO: If used in text mode, should expand to \textellipsis.
            // However, in KaTeX, \textellipsis and \ldots behave the same
            // (in text mode), and it's unlikely we'd see any of the math commands
            // that affect the behavior of \dots when in text mode.  So fine for now
            // (until we support \ifmmode ... \else ... \fi).
            object thedots = "\\dotso";
            var next = context.ExpandAfterFuture().Text;
            if (DotsByToken.TryGetValue(next, out var dots))
            {
                thedots = dots;
            }
            else if (next.StartsWith("\\not", StringComparison.Ordinal))
            {
                thedots = "\\dotsb";
            }
            else
            {
                var symbol = Symbols.Get("math", next);
                if (symbol != null)
                {
                    if (DotsbGroups.Contains(symbol.Group))
                    {
                        thedots = "\\dotsb";
                    }
                }
            }
            return thedots;
        }));

        Def("\\dotso", (MacroFunction)(context =>
        {
            var next = context.Future().Text;
            if (SpaceAfterDots.ContainsKey(next))
            {
                return "\\ldots\\,";
            }
            else
            {
                return "\\ldots";
            }
        }));

        Def("\\dotsc", (MacroFunction)(context =>
        {
            var next = context.Future().Text;
            // \dotsc uses \extra@ but not \extrap@, instead specially checking for
            // ';' and '.', but doesn't check for ','.
            if (SpaceAfterDots.ContainsKey(next) && next != ",")
            {
                return "\\ldots\\,";
            }
            else
            {
                return "\\ldots";
            }
        }));

        Def("\\cdots", (MacroFunction)(context =>
        {
            var next = context.Future().Text;
            if (SpaceAfterDots.ContainsKey(next))
            {
                return "\\@cdots\\,";
            }
            else
            {
                return "\\@cdots";
            }
        }));

        Def("\\dotsb", "\\cdots");
        Def("\\dotsm", "\\cdots");
        Def("\\dotsi", "\\!\\cdots");
        // amsmath doesn't actually define \dotsx, but \dots followed by a macro
        // starting with \DOTSX implies \dotso, and then \extra@ detects this case
        // and forces the added `\,`.
        Def("\\dotsx", "\\ldots\\,");

        // \let\DOTSI\relax
        // \let\DOTSB\relax
        // \let\DOTSX\relax
        Def("\\DOTSI", "\\relax");
        Def("\\DOTSB", "\\relax");
        Def("\\DOTSX", "\\relax");

        // Spacing, based on amsmath.sty's override of LaTeX defaults
        // \DeclareRobustCommand{\tmspace}[3]{%
        //   \ifmmode\mskip#1#2\else\kern#1#3\fi\relax}
        Def("\\tmspace", "\\TextOrMath{\\kern#1#3}{\\mskip#1#2}\\relax");
        // \renewcommand{\,}{\tmspace+\thinmuskip{.1667em}}
        // TODO: math mode should use \thinmuskip
        Def("\\,", "\\tmspace+{3mu}{.1667em}");
        // \let\thinspace\,
        Def("\\thinspace", "\\,");
        // \def\>{\mskip\medmuskip}
        // \renewcommand{\:}{\tmspace+\medmuskip{.2222em}}
        // TODO: \> and math mode of \: should use \medmuskip = 4mu plus 2mu minus 4mu
        Def("\\>", "\\mskip{4mu}");
        Def("\\:", "\\tmspace+{4mu}{.2222em}");
        // \let\medspace\:
        Def("\\medspace", "\\:");
        // \renewcommand{\;}{\tmspace+\thickmuskip{.2777em}}
        // TODO: math mode should use \thickmuskip = 5mu plus 5mu
        Def("\\;", "\\tmspace+{5mu}{.2777em}");
        // \let\thickspace\;
        Def("\\thickspace", "\\;");
        // \renewcommand{\!}{\tmspace-\thinmuskip{.1667em}}
        // TODO: math mode should use \thinmuskip
        Def("\\!", "\\tmspace-{3mu}{.1667em}");
        // \let\negthinspace\!
        Def("\\negthinspace", "\\!");
        // \newcommand{\negmedspace}{\tmspace-\medmuskip{.2222em}}
        // TODO: math mode should use \medmuskip
        Def("\\negmedspace", "\\tmspace-{4mu}{.2222em}");
        // \newcommand{\negthickspace}{\tmspace-\thickmuskip{.2777em}}
        // TODO: math mode should use \thickmuskip
        Def("\\negthickspace", "\\tmspace-{5mu}{.277em}");
        // \def\enspace{\kern.5em }
        Def("\\enspace", "\\kern.5em ");
        // \def\enskip{\hskip.5em\relax}
        Def("\\enskip", "\\hskip.5em\\relax");
        // \def\quad{\hskip1em\relax}
        Def("\\quad", "\\hskip1em\\relax");
        // \def\qquad{\hskip2em\relax}
        Def("\\qquad", "\\hskip2em\\relax");

        // \tag@in@display form of \tag
        Def("\\tag", "\\@ifstar\\tag@literal\\tag@paren");
        Def("\\tag@paren", "\\tag@literal{({#1})}");
        Def("\\tag@literal", (MacroFunction)(context =>
        {
            if (MacroDefinitions.Truthy(context.Macros.Get("\\df@tag")))
            {
                throw new ParseError("Multiple \\tag");
            }
            return "\\gdef\\df@tag{\\text{#1}}";
        }));

        // \renewcommand{\bmod}{\nonscript\mskip-\medmuskip\mkern5mu\mathbin
        //   {\operator@font mod}\penalty900
        //   \mkern5mu\nonscript\mskip-\medmuskip}
        // \newcommand{\pod}[1]{\allowbreak
        //   \if@display\mkern18mu\else\mkern8mu\fi(#1)}
        // \renewcommand{\pmod}[1]{\pod{{\operator@font mod}\mkern6mu#1}}
        // \newcommand{\mod}[1]{\allowbreak\if@display\mkern18mu
        //   \else\mkern12mu\fi{\operator@font mod}\,\,#1}
        // TODO: math mode should use \medmuskip = 4mu plus 2mu minus 4mu
        Def("\\bmod",
            "\\mathchoice{\\mskip1mu}{\\mskip1mu}{\\mskip5mu}{\\mskip5mu}" +
            "\\mathbin{\\rm mod}" +
            "\\mathchoice{\\mskip1mu}{\\mskip1mu}{\\mskip5mu}{\\mskip5mu}");
        Def("\\pod", "\\allowbreak" +
            "\\mathchoice{\\mkern18mu}{\\mkern8mu}{\\mkern8mu}{\\mkern8mu}(#1)");
        Def("\\pmod", "\\pod{{\\rm mod}\\mkern6mu#1}");
        Def("\\mod", "\\allowbreak" +
            "\\mathchoice{\\mkern18mu}{\\mkern12mu}{\\mkern12mu}{\\mkern12mu}" +
            "{\\rm mod}\\,\\,#1");

        //////////////////////////////////////////////////////////////////////
        // LaTeX source2e

        // \expandafter\let\expandafter\@normalcr
        //     \csname\expandafter\@gobble\string\\ \endcsname
        // \DeclareRobustCommand\newline{\@normalcr\relax}
        Def("\\newline", "\\\\\\relax");

        // \def\TeX{T\kern-.1667em\lower.5ex\hbox{E}\kern-.125emX\@}
        // TODO: Doesn't normally work in math mode because \@ fails.  KaTeX doesn't
        // support \@ yet, so that's omitted, and we add \text so that the result
        // doesn't look funny in math mode.
        Def("\\TeX", "\\textrm{\\html@mathml{" +
            "T\\kern-.1667em\\raisebox{-.5ex}{E}\\kern-.125emX" +
            "}{TeX}}");

        // \DeclareRobustCommand{\LaTeX}{L\kern-.36em%
        //         {\sbox\z@ T%
        //          \vbox to\ht\z@{\hbox{\check@mathfonts
        //                               \fontsize\sf@size\z@
        //                               \math@fontsfalse\selectfont
        //                               A}%
        //                         \vss}%
        //         }%
        //         \kern-.15em%
        //         \TeX}
        // This code aligns the top of the A with the T (from the perspective of TeX's
        // boxes, though visually the A appears to extend above slightly).
        // We compute the corresponding \raisebox when A is rendered in \normalsize
        // \scriptstyle, which has a scale factor of 0.7 (see Options.js).
        var latexRaiseA = Units.MakeEm(FontMetricsData.Map["Main-Regular"]['T'][1] -
            0.7 * FontMetricsData.Map["Main-Regular"]['A'][1]);
        Def("\\LaTeX", "\\textrm{\\html@mathml{" +
            "L\\kern-.36em\\raisebox{" + latexRaiseA + "}{\\scriptstyle A}" +
            "\\kern-.15em\\TeX}{LaTeX}}");

        // New KaTeX logo based on tweaking LaTeX logo
        Def("\\KaTeX", "\\textrm{\\html@mathml{" +
            "K\\kern-.17em\\raisebox{" + latexRaiseA + "}{\\scriptstyle A}" +
            "\\kern-.15em\\TeX}{KaTeX}}");

        // \DeclareRobustCommand\hspace{\@ifstar\@hspacer\@hspace}
        // \def\@hspace#1{\hskip  #1\relax}
        // \def\@hspacer#1{\vrule \@width\z@\nobreak
        //                 \hskip #1\hskip \@z@skip}
        Def("\\hspace", "\\@ifstar\\@hspacer\\@hspace");
        Def("\\@hspace", "\\hskip #1\\relax");
        Def("\\@hspacer", "\\rule{0pt}{0pt}\\hskip #1\\relax");

        //////////////////////////////////////////////////////////////////////
        // mathtools.sty

        //\providecommand\ordinarycolon{:}
        Def("\\ordinarycolon", ":");
        //\def\vcentcolon{\mathrel{\mathop\ordinarycolon}}
        //TODO(edemaine): Not yet centered. Fix via \raisebox or #726
        Def("\\vcentcolon", "\\mathrel{\\mathop\\ordinarycolon}");
        // \providecommand*\dblcolon{\vcentcolon\mathrel{\mkern-.9mu}\vcentcolon}
        Def("\\dblcolon", "\\html@mathml{" +
            "\\mathrel{\\vcentcolon\\mathrel{\\mkern-.9mu}\\vcentcolon}}" +
            "{\\mathop{\\char\"2237}}");
        // \providecommand*\coloneqq{\vcentcolon\mathrel{\mkern-1.2mu}=}
        Def("\\coloneqq", "\\html@mathml{" +
            "\\mathrel{\\vcentcolon\\mathrel{\\mkern-1.2mu}=}}" +
            "{\\mathop{\\char\"2254}}"); // \u2254
        // \providecommand*\Coloneqq{\dblcolon\mathrel{\mkern-1.2mu}=}
        Def("\\Coloneqq", "\\html@mathml{" +
            "\\mathrel{\\dblcolon\\mathrel{\\mkern-1.2mu}=}}" +
            "{\\mathop{\\char\"2237\\char\"3d}}");
        // \providecommand*\coloneq{\vcentcolon\mathrel{\mkern-1.2mu}\mathrel{-}}
        Def("\\coloneq", "\\html@mathml{" +
            "\\mathrel{\\vcentcolon\\mathrel{\\mkern-1.2mu}\\mathrel{-}}}" +
            "{\\mathop{\\char\"3a\\char\"2212}}");
        // \providecommand*\Coloneq{\dblcolon\mathrel{\mkern-1.2mu}\mathrel{-}}
        Def("\\Coloneq", "\\html@mathml{" +
            "\\mathrel{\\dblcolon\\mathrel{\\mkern-1.2mu}\\mathrel{-}}}" +
            "{\\mathop{\\char\"2237\\char\"2212}}");
        // \providecommand*\eqqcolon{=\mathrel{\mkern-1.2mu}\vcentcolon}
        Def("\\eqqcolon", "\\html@mathml{" +
            "\\mathrel{=\\mathrel{\\mkern-1.2mu}\\vcentcolon}}" +
            "{\\mathop{\\char\"2255}}"); // \u2255
        // \providecommand*\Eqqcolon{=\mathrel{\mkern-1.2mu}\dblcolon}
        Def("\\Eqqcolon", "\\html@mathml{" +
            "\\mathrel{=\\mathrel{\\mkern-1.2mu}\\dblcolon}}" +
            "{\\mathop{\\char\"3d\\char\"2237}}");
        // \providecommand*\eqcolon{\mathrel{-}\mathrel{\mkern-1.2mu}\vcentcolon}
        Def("\\eqcolon", "\\html@mathml{" +
            "\\mathrel{\\mathrel{-}\\mathrel{\\mkern-1.2mu}\\vcentcolon}}" +
            "{\\mathop{\\char\"2239}}");
        // \providecommand*\Eqcolon{\mathrel{-}\mathrel{\mkern-1.2mu}\dblcolon}
        Def("\\Eqcolon", "\\html@mathml{" +
            "\\mathrel{\\mathrel{-}\\mathrel{\\mkern-1.2mu}\\dblcolon}}" +
            "{\\mathop{\\char\"2212\\char\"2237}}");
        // \providecommand*\colonapprox{\vcentcolon\mathrel{\mkern-1.2mu}\approx}
        Def("\\colonapprox", "\\html@mathml{" +
            "\\mathrel{\\vcentcolon\\mathrel{\\mkern-1.2mu}\\approx}}" +
            "{\\mathop{\\char\"3a\\char\"2248}}");
        // \providecommand*\Colonapprox{\dblcolon\mathrel{\mkern-1.2mu}\approx}
        Def("\\Colonapprox", "\\html@mathml{" +
            "\\mathrel{\\dblcolon\\mathrel{\\mkern-1.2mu}\\approx}}" +
            "{\\mathop{\\char\"2237\\char\"2248}}");
        // \providecommand*\colonsim{\vcentcolon\mathrel{\mkern-1.2mu}\sim}
        Def("\\colonsim", "\\html@mathml{" +
            "\\mathrel{\\vcentcolon\\mathrel{\\mkern-1.2mu}\\sim}}" +
            "{\\mathop{\\char\"3a\\char\"223c}}");
        // \providecommand*\Colonsim{\dblcolon\mathrel{\mkern-1.2mu}\sim}
        Def("\\Colonsim", "\\html@mathml{" +
            "\\mathrel{\\dblcolon\\mathrel{\\mkern-1.2mu}\\sim}}" +
            "{\\mathop{\\char\"2237\\char\"223c}}");

        // Some Unicode characters are implemented with macros to mathtools functions.
        Def("\u2237", "\\dblcolon");  // ::
        Def("\u2239", "\\eqcolon");  // -:
        Def("\u2254", "\\coloneqq");  // :=
        Def("\u2255", "\\eqqcolon");  // =:
        Def("\u2A74", "\\Coloneqq");  // ::=

        //////////////////////////////////////////////////////////////////////
        // colonequals.sty

        // Alternate names for mathtools's macros:
        Def("\\ratio", "\\vcentcolon");
        Def("\\coloncolon", "\\dblcolon");
        Def("\\colonequals", "\\coloneqq");
        Def("\\coloncolonequals", "\\Coloneqq");
        Def("\\equalscolon", "\\eqqcolon");
        Def("\\equalscoloncolon", "\\Eqqcolon");
        Def("\\colonminus", "\\coloneq");
        Def("\\coloncolonminus", "\\Coloneq");
        Def("\\minuscolon", "\\eqcolon");
        Def("\\minuscoloncolon", "\\Eqcolon");
        // \colonapprox name is same in mathtools and colonequals.
        Def("\\coloncolonapprox", "\\Colonapprox");
        // \colonsim name is same in mathtools and colonequals.
        Def("\\coloncolonsim", "\\Colonsim");

        // Additional macros, implemented by analogy with mathtools definitions:
        Def("\\simcolon",
            "\\mathrel{\\sim\\mathrel{\\mkern-1.2mu}\\vcentcolon}");
        Def("\\simcoloncolon",
            "\\mathrel{\\sim\\mathrel{\\mkern-1.2mu}\\dblcolon}");
        Def("\\approxcolon",
            "\\mathrel{\\approx\\mathrel{\\mkern-1.2mu}\\vcentcolon}");
        Def("\\approxcoloncolon",
                    "\\mathrel{\\approx\\mathrel{\\mkern-1.2mu}\\dblcolon}");

        // Present in newtxmath, pxfonts and txfonts
        Def("\\notni", "\\html@mathml{\\not\\ni}{\\mathrel{\\char`\u220C}}");
        Def("\\limsup", "\\DOTSB\\operatorname*{lim\\,sup}");
        Def("\\liminf", "\\DOTSB\\operatorname*{lim\\,inf}");

        //////////////////////////////////////////////////////////////////////
        // From amsopn.sty
        Def("\\injlim", "\\DOTSB\\operatorname*{inj\\,lim}");
        Def("\\projlim", "\\DOTSB\\operatorname*{proj\\,lim}");
        Def("\\varlimsup", "\\DOTSB\\operatorname*{\\overline{lim}}");
        Def("\\varliminf", "\\DOTSB\\operatorname*{\\underline{lim}}");
        Def("\\varinjlim", "\\DOTSB\\operatorname*{\\underrightarrow{lim}}");
        Def("\\varprojlim", "\\DOTSB\\operatorname*{\\underleftarrow{lim}}");

        //////////////////////////////////////////////////////////////////////
        // MathML alternates for KaTeX glyphs in the Unicode private area
        Def("\\gvertneqq", "\\html@mathml{\\@gvertneqq}{\u2269}");
        Def("\\lvertneqq", "\\html@mathml{\\@lvertneqq}{\u2268}");
        Def("\\ngeqq", "\\html@mathml{\\@ngeqq}{\u2271}");
        Def("\\ngeqslant", "\\html@mathml{\\@ngeqslant}{\u2271}");
        Def("\\nleqq", "\\html@mathml{\\@nleqq}{\u2270}");
        Def("\\nleqslant", "\\html@mathml{\\@nleqslant}{\u2270}");
        Def("\\nshortmid", "\\html@mathml{\\@nshortmid}{\u2224}");
        Def("\\nshortparallel", "\\html@mathml{\\@nshortparallel}{\u2226}");
        Def("\\nsubseteqq", "\\html@mathml{\\@nsubseteqq}{\u2288}");
        Def("\\nsupseteqq", "\\html@mathml{\\@nsupseteqq}{\u2289}");
        Def("\\varsubsetneq", "\\html@mathml{\\@varsubsetneq}{\u228A}");
        Def("\\varsubsetneqq", "\\html@mathml{\\@varsubsetneqq}{\u2ACB}");
        Def("\\varsupsetneq", "\\html@mathml{\\@varsupsetneq}{\u228B}");
        Def("\\varsupsetneqq", "\\html@mathml{\\@varsupsetneqq}{\u2ACC}");
        Def("\\imath", "\\html@mathml{\\@imath}{\u0131}");
        Def("\\jmath", "\\html@mathml{\\@jmath}{\u0237}");

        //////////////////////////////////////////////////////////////////////
        // stmaryrd and semantic

        // The stmaryrd and semantic packages render the next four items by calling a
        // glyph. Those glyphs do not exist in the KaTeX fonts. Hence the macros.

        Def("\\llbracket", "\\html@mathml{" +
            "\\mathopen{[\\mkern-3.2mu[}}" +
            "{\\mathopen{\\char`\u27e6}}");
        Def("\\rrbracket", "\\html@mathml{" +
            "\\mathclose{]\\mkern-3.2mu]}}" +
            "{\\mathclose{\\char`\u27e7}}");

        Def("\u27e6", "\\llbracket"); // blackboard bold [
        Def("\u27e7", "\\rrbracket"); // blackboard bold ]

        Def("\\lBrace", "\\html@mathml{" +
            "\\mathopen{\\{\\mkern-3.2mu[}}" +
            "{\\mathopen{\\char`\u2983}}");
        Def("\\rBrace", "\\html@mathml{" +
            "\\mathclose{]\\mkern-3.2mu\\}}}" +
            "{\\mathclose{\\char`\u2984}}");

        Def("\u21A4", "\\mapsfrom");
        Def(
            "\\mapsfrom",
            "\\html@mathml{\\mathrel{\\mathreflectbox{\\mapsto}}}{\\mathrel{\\char`\u21A4}}"
        );

        Def("\u2983", "\\lBrace"); // blackboard bold {
        Def("\u2984", "\\rBrace"); // blackboard bold }

        // TODO: Create variable sized versions of the last two items. I believe that
        // will require new font glyphs.

        // The stmaryrd function `\minuso` provides a "Plimsoll" symbol that
        // superimposes the characters \circ and \mathminus. Used in chemistry.
        Def("\\minuso", "\\mathbin{\\html@mathml{" +
            "{\\mathrlap{\\mathchoice{\\kern{0.145em}}{\\kern{0.145em}}" +
            "{\\kern{0.1015em}}{\\kern{0.0725em}}\\circ}{-}}}" +
            "{\\char`\u29B5}}");
        Def("\u29B5", "\\minuso");

        //////////////////////////////////////////////////////////////////////
        // texvc.sty

        // The texvc package contains macros available in mediawiki pages.
        // We omit the functions deprecated at
        // https://en.wikipedia.org/wiki/Help:Displaying_a_formula#Deprecated_syntax

        // We also omit texvc's \O, which conflicts with \text{\O}

        Def("\\darr", "\\downarrow");
        Def("\\dArr", "\\Downarrow");
        Def("\\Darr", "\\Downarrow");
        Def("\\lang", "\\langle");
        Def("\\rang", "\\rangle");
        Def("\\uarr", "\\uparrow");
        Def("\\uArr", "\\Uparrow");
        Def("\\Uarr", "\\Uparrow");
        Def("\\N", "\\mathbb{N}");
        Def("\\R", "\\mathbb{R}");
        Def("\\Z", "\\mathbb{Z}");
        Def("\\alef", "\\aleph");
        Def("\\alefsym", "\\aleph");
        Def("\\Alpha", "\\mathrm{A}");
        Def("\\Beta", "\\mathrm{B}");
        Def("\\bull", "\\bullet");
        Def("\\Chi", "\\mathrm{X}");
        Def("\\clubs", "\\clubsuit");
        Def("\\cnums", "\\mathbb{C}");
        Def("\\Complex", "\\mathbb{C}");
        Def("\\Dagger", "\\ddagger");
        Def("\\diamonds", "\\diamondsuit");
        Def("\\empty", "\\emptyset");
        Def("\\Epsilon", "\\mathrm{E}");
        Def("\\Eta", "\\mathrm{H}");
        Def("\\exist", "\\exists");
        Def("\\harr", "\\leftrightarrow");
        Def("\\hArr", "\\Leftrightarrow");
        Def("\\Harr", "\\Leftrightarrow");
        Def("\\hearts", "\\heartsuit");
        Def("\\image", "\\Im");
        Def("\\infin", "\\infty");
        Def("\\Iota", "\\mathrm{I}");
        Def("\\isin", "\\in");
        Def("\\Kappa", "\\mathrm{K}");
        Def("\\larr", "\\leftarrow");
        Def("\\lArr", "\\Leftarrow");
        Def("\\Larr", "\\Leftarrow");
        Def("\\lrarr", "\\leftrightarrow");
        Def("\\lrArr", "\\Leftrightarrow");
        Def("\\Lrarr", "\\Leftrightarrow");
        Def("\\Mu", "\\mathrm{M}");
        Def("\\natnums", "\\mathbb{N}");
        Def("\\Nu", "\\mathrm{N}");
        Def("\\Omicron", "\\mathrm{O}");
        Def("\\plusmn", "\\pm");
        Def("\\rarr", "\\rightarrow");
        Def("\\rArr", "\\Rightarrow");
        Def("\\Rarr", "\\Rightarrow");
        Def("\\real", "\\Re");
        Def("\\reals", "\\mathbb{R}");
        Def("\\Reals", "\\mathbb{R}");
        Def("\\Rho", "\\mathrm{P}");
        Def("\\sdot", "\\cdot");
        Def("\\sect", "\\S");
        Def("\\spades", "\\spadesuit");
        Def("\\sub", "\\subset");
        Def("\\sube", "\\subseteq");
        Def("\\supe", "\\supseteq");
        Def("\\Tau", "\\mathrm{T}");
        Def("\\thetasym", "\\vartheta");
        // TODO: defineMacro("\\varcoppa", "\\\mbox{\\coppa}");
        Def("\\weierp", "\\wp");
        Def("\\Zeta", "\\mathrm{Z}");

        //////////////////////////////////////////////////////////////////////
        // statmath.sty
        // https://ctan.math.illinois.edu/macros/latex/contrib/statmath/statmath.pdf

        Def("\\argmin", "\\DOTSB\\operatorname*{arg\\,min}");
        Def("\\argmax", "\\DOTSB\\operatorname*{arg\\,max}");
        Def("\\plim", "\\DOTSB\\mathop{\\operatorname{plim}}\\limits");

        //////////////////////////////////////////////////////////////////////
        // braket.sty
        // http://ctan.math.washington.edu/tex-archive/macros/latex/contrib/braket/braket.pdf

        Def("\\bra", "\\mathinner{\\langle{#1}|}");
        Def("\\ket", "\\mathinner{|{#1}\\rangle}");
        Def("\\braket", "\\mathinner{\\langle{#1}\\rangle}");
        Def("\\Bra", "\\left\\langle#1\\right|");
        Def("\\Ket", "\\left|#1\\right\\rangle");
        Def("\\bra@ket", BraketHelper(false));
        Def("\\bra@set", BraketHelper(true));
        Def("\\Braket", "\\bra@ket{\\left\\langle}" +
            "{\\,\\middle\\vert\\,}{\\,\\middle\\vert\\,}{\\right\\rangle}");
        Def("\\Set", "\\bra@set{\\left\\{\\:}" +
            "{\\;\\middle\\vert\\;}{\\;\\middle\\Vert\\;}{\\:\\right\\}}");
        Def("\\set", "\\bra@set{\\{\\,}{\\mid}{}{\\,\\}}");
            // has no support for special || or \|

        //////////////////////////////////////////////////////////////////////
        // actuarialangle.dtx
        Def("\\angln", "{\\angl n}");

        // Custom Khan Academy colors, should be moved to an optional package
        Def("\\blue", "\\textcolor{##6495ed}{#1}");
        Def("\\orange", "\\textcolor{##ffa500}{#1}");
        Def("\\pink", "\\textcolor{##ff00af}{#1}");
        Def("\\red", "\\textcolor{##df0030}{#1}");
        Def("\\green", "\\textcolor{##28ae7b}{#1}");
        Def("\\gray", "\\textcolor{gray}{#1}");
        Def("\\purple", "\\textcolor{##9d38bd}{#1}");
        Def("\\blueA", "\\textcolor{##ccfaff}{#1}");
        Def("\\blueB", "\\textcolor{##80f6ff}{#1}");
        Def("\\blueC", "\\textcolor{##63d9ea}{#1}");
        Def("\\blueD", "\\textcolor{##11accd}{#1}");
        Def("\\blueE", "\\textcolor{##0c7f99}{#1}");
        Def("\\tealA", "\\textcolor{##94fff5}{#1}");
        Def("\\tealB", "\\textcolor{##26edd5}{#1}");
        Def("\\tealC", "\\textcolor{##01d1c1}{#1}");
        Def("\\tealD", "\\textcolor{##01a995}{#1}");
        Def("\\tealE", "\\textcolor{##208170}{#1}");
        Def("\\greenA", "\\textcolor{##b6ffb0}{#1}");
        Def("\\greenB", "\\textcolor{##8af281}{#1}");
        Def("\\greenC", "\\textcolor{##74cf70}{#1}");
        Def("\\greenD", "\\textcolor{##1fab54}{#1}");
        Def("\\greenE", "\\textcolor{##0d923f}{#1}");
        Def("\\goldA", "\\textcolor{##ffd0a9}{#1}");
        Def("\\goldB", "\\textcolor{##ffbb71}{#1}");
        Def("\\goldC", "\\textcolor{##ff9c39}{#1}");
        Def("\\goldD", "\\textcolor{##e07d10}{#1}");
        Def("\\goldE", "\\textcolor{##a75a05}{#1}");
        Def("\\redA", "\\textcolor{##fca9a9}{#1}");
        Def("\\redB", "\\textcolor{##ff8482}{#1}");
        Def("\\redC", "\\textcolor{##f9685d}{#1}");
        Def("\\redD", "\\textcolor{##e84d39}{#1}");
        Def("\\redE", "\\textcolor{##bc2612}{#1}");
        Def("\\maroonA", "\\textcolor{##ffbde0}{#1}");
        Def("\\maroonB", "\\textcolor{##ff92c6}{#1}");
        Def("\\maroonC", "\\textcolor{##ed5fa6}{#1}");
        Def("\\maroonD", "\\textcolor{##ca337c}{#1}");
        Def("\\maroonE", "\\textcolor{##9e034e}{#1}");
        Def("\\purpleA", "\\textcolor{##ddd7ff}{#1}");
        Def("\\purpleB", "\\textcolor{##c6b9fc}{#1}");
        Def("\\purpleC", "\\textcolor{##aa87ff}{#1}");
        Def("\\purpleD", "\\textcolor{##7854ab}{#1}");
        Def("\\purpleE", "\\textcolor{##543b78}{#1}");
        Def("\\mintA", "\\textcolor{##f5f9e8}{#1}");
        Def("\\mintB", "\\textcolor{##edf2df}{#1}");
        Def("\\mintC", "\\textcolor{##e0e5cc}{#1}");
        Def("\\grayA", "\\textcolor{##f6f7f7}{#1}");
        Def("\\grayB", "\\textcolor{##f0f1f2}{#1}");
        Def("\\grayC", "\\textcolor{##e3e5e6}{#1}");
        Def("\\grayD", "\\textcolor{##d6d8da}{#1}");
        Def("\\grayE", "\\textcolor{##babec2}{#1}");
        Def("\\grayF", "\\textcolor{##888d93}{#1}");
        Def("\\grayG", "\\textcolor{##626569}{#1}");
        Def("\\grayH", "\\textcolor{##3b3e40}{#1}");
        Def("\\grayI", "\\textcolor{##21242c}{#1}");
        Def("\\kaBlue", "\\textcolor{##314453}{#1}");
        Def("\\kaGreen", "\\textcolor{##71B307}{#1}");
    }

    // \newcommand{\macro}[args]{definition}
    // \renewcommand{\macro}[args]{definition}
    // TODO: Optional arguments: \newcommand{\macro}[args][default]{definition}
    private static object Newcommand(
        MacroContextInterface context,
        bool existsOK,
        bool nonexistsOK,
        bool skipIfExists)
    {
        var arg = context.ConsumeArg().Tokens;
        if (arg.Count != 1)
        {
            throw new ParseError(
                "\\newcommand's first argument must be a macro name");
        }
        var name = arg[0].Text;

        var exists = context.IsDefined(name);
        if (exists && !existsOK)
        {
            throw new ParseError($"\\newcommand{{{name}}} attempting to redefine " +
                $"{name}; use \\renewcommand");
        }
        if (!exists && !nonexistsOK)
        {
            throw new ParseError($"\\renewcommand{{{name}}} when command {name} " +
                "does not yet exist; use \\newcommand");
        }

        int numArgs = 0;
        arg = context.ConsumeArg().Tokens;
        if (arg.Count == 1 && arg[0].Text == "[")
        {
            var argText = "";
            var token = context.ExpandNextToken();
            while (token.Text != "]" && token.Text != "EOF")
            {
                // TODO: Should properly expand arg, e.g., ignore {}s
                argText += token.Text;
                token = context.ExpandNextToken();
            }
            if (!ValidNumberOfArguments.IsMatch(argText))
            {
                throw new ParseError($"Invalid number of arguments: {argText}");
            }
            numArgs = int.Parse(argText.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            arg = context.ConsumeArg().Tokens;
        }

        if (!(exists && skipIfExists))
        {
            // Final arg is the expansion of the macro
            context.Macros.Set(name, new MacroExpansion
            {
                Tokens = arg,
                NumArgs = numArgs,
            });
        }
        return "";
    }

    private static MacroFunction BraketHelper(bool one) => (context) =>
    {
        var left = context.ConsumeArg().Tokens;
        var middle = context.ConsumeArg().Tokens;
        var middleDouble = context.ConsumeArg().Tokens;
        var right = context.ConsumeArg().Tokens;
        var oldMiddle = context.Macros.Get("|");
        var oldMiddleDouble = context.Macros.Get("\\|");
        context.Macros.BeginGroup();

        MacroFunction MidMacro(bool doubleValue) => (ctx) =>
        {
            if (one)
            {
                // Only modify the first instance of | or \|
                ctx.Macros.Set("|", oldMiddle);
                if (middleDouble.Count > 0)
                {
                    ctx.Macros.Set("\\|", oldMiddleDouble);
                }
            }
            var doubled = doubleValue;
            if (!doubleValue && middleDouble.Count > 0)
            {
                // Mimic \@ifnextchar
                var nextToken = ctx.Future();
                if (nextToken.Text == "|")
                {
                    ctx.PopToken();
                    doubled = true;
                }
            }
            return new MacroExpansion
            {
                Tokens = doubled ? middleDouble : middle,
                NumArgs = 0,
            };
        };

        context.Macros.Set("|", MidMacro(false));
        if (middleDouble.Count > 0)
        {
            context.Macros.Set("\\|", MidMacro(true));
        }
        var arg = context.ConsumeArg().Tokens;
        var expanded = context.ExpandTokens(new List<Token>(right.Concat(arg).Concat(left)));
        context.Macros.EndGroup();
        expanded.Reverse();
        return new MacroExpansion
        {
            Tokens = expanded,
            NumArgs = 0,
        };
    };

    public static bool Truthy(object value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        _ => true,
    };

    private static string FormatMacro(object definition) => definition switch
    {
        null => "undefined",
        string s => s,
        MacroExpansion e => "MacroExpansion(" + string.Concat(e.Tokens.Select(t => t.Text)) + ")",
        _ => definition.ToString(),
    };

    private static string FormatSymbol(SymbolInfo info)
        => info == null ? "undefined" : info.Font + "/" + info.Group;
}




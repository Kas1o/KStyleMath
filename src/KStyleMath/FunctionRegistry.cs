namespace KStyleMath;

public static class FunctionRegistry
{
    private static bool _defined;
    private static readonly object _gate = new();

    public static void DefineAll()
    {
        lock (_gate)
        {
            if (_defined) return;
            _defined = true;

            // Order mirrors src/functions.ts in KaTeX.
            AccentFunctions.Define();
            AccentUnderFunctions.Define();
            ArrowFunctions.Define();
            PmbFunctions.Define();
            CdFunctions.Define();
            CharFunctions.Define();
            ColorFunctions.Define();
            CrFunctions.Define();
            DefFunctions.Define();
            DelimsizingFunctions.Define();
            EncloseFunctions.Define();
            EnvironmentFunctions.Define();
            FontFunctions.Define();
            GenfracFunctions.Define();
            HorizBraceFunctions.Define();
            HrefFunctions.Define();
            HboxFunctions.Define();
            HtmlFunctions.Define();
            HtmlMathmlFunctions.Define();
            IncludegraphicsFunctions.Define();
            KernFunctions.Define();
            LapFunctions.Define();
            MathFunctions.Define();
            MathchoiceFunctions.Define();
            MclassFunctions.Define();
            OpFunctions.Define();
            OperatornameFunctions.Define();
            OrdgroupFunctions.Define();
            OverlineFunctions.Define();
            PhantomFunctions.Define();
            RaiseboxFunctions.Define();
            ReflectboxFunctions.Define();
            RelaxFunctions.Define();
            RuleFunctions.Define();
            SizingFunctions.Define();
            SmashFunctions.Define();
            SqrtFunctions.Define();
            StylingFunctions.Define();
            SupsubFunctions.Define();
            SymbolsOpFunctions.Define();
            SymbolsOrdFunctions.Define();
            SymbolsSpacingFunctions.Define();
            TagFunctions.Define();
            TextFunctions.Define();
            UnderlineFunctions.Define();
            VcenterFunctions.Define();
            VerbFunctions.Define();
            ArrayFunctions.Define();
        }
    }
}

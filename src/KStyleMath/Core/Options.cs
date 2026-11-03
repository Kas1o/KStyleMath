namespace KStyleMath;

public sealed class FontMetrics
{
    public double Slant;
    public double Space;
    public double Stretch;
    public double Shrink;
    public double XHeight;
    public double Quad;
    public double ExtraSpace;
    public double Num1;
    public double Num2;
    public double Num3;
    public double Denom1;
    public double Denom2;
    public double Sup1;
    public double Sup2;
    public double Sup3;
    public double Sub1;
    public double Sub2;
    public double SupDrop;
    public double SubDrop;
    public double Delim1;
    public double Delim2;
    public double AxisHeight;
    public double DefaultRuleThickness;
    public double BigOpSpacing1;
    public double BigOpSpacing2;
    public double BigOpSpacing3;
    public double BigOpSpacing4;
    public double BigOpSpacing5;
    public double SqrtRuleThickness;
    public double PtPerEm;
    public double DoubleRuleSep;
    public double ArrayRuleWidth;
    public double Fboxsep;
    public double Fboxrule;
    public double CssEmPerMu;
}

public sealed class Options
{
    public Style Style;
    public string Color;
    public int Size;
    public int TextSize;
    public bool Phantom;
    public string Font;
    public string FontFamily;
    public string FontWeight;
    public string FontShape;
    public double SizeMultiplier;
    public double MaxSize;
    public double MinRuleThickness;
    public FontMetrics _fontMetrics;
    public object Strict;

    public const int BaseSize = 6;

    private static readonly int[][] SizeStyleMap =
    {
        new[] {1, 1, 1}, new[] {2, 1, 1}, new[] {3, 1, 1}, new[] {4, 2, 1},
        new[] {5, 2, 1}, new[] {6, 3, 1}, new[] {7, 4, 2}, new[] {8, 6, 3},
        new[] {9, 7, 6}, new[] {10, 8, 7}, new[] {11, 10, 9},
    };

    private static readonly double[] SizeMultipliers =
    {
        0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.44, 1.728, 2.074, 2.488,
    };

    public static int SizeAtStyle(int size, Style style)
        => style.Size < 2 ? size : SizeStyleMap[size - 1][style.Size - 1];

    public Options(Style style, string color = null, int size = 0, int? textSize = null,
        bool phantom = false, string font = "", string fontFamily = "", string fontWeight = "",
        string fontShape = "", double? sizeMultiplier = null, double maxSize = double.PositiveInfinity,
        double minRuleThickness = 0, object strict = null)
    {
        Style = style;
        Color = color;
        Size = size == 0 ? BaseSize : size;
        TextSize = textSize ?? Size;
        Phantom = phantom;
        Font = font ?? "";
        FontFamily = fontFamily ?? "";
        FontWeight = fontWeight ?? "";
        FontShape = fontShape ?? "";
        SizeMultiplier = sizeMultiplier ?? SizeMultipliers[Size - 1];
        MaxSize = maxSize;
        MinRuleThickness = minRuleThickness;
        Strict = strict;
    }

    public Options Extend(Style style = null, string color = null, int? size = null, int? textSize = null,
        bool? phantom = null, string font = null, string fontFamily = null, string fontWeight = null,
        string fontShape = null, double? sizeMultiplier = null, double? maxSize = null,
        double? minRuleThickness = null, object strict = null, bool keepColor = true)
    {
        return new Options(
            style ?? Style,
            color ?? (keepColor ? Color : null),
            size ?? Size,
            textSize ?? TextSize,
            phantom ?? Phantom,
            font ?? Font,
            fontFamily ?? FontFamily,
            fontWeight ?? FontWeight,
            fontShape ?? FontShape,
            sizeMultiplier ?? SizeMultipliers[(size ?? Size) - 1],
            maxSize ?? MaxSize,
            minRuleThickness ?? MinRuleThickness,
            strict ?? Strict);
    }

    public Options HavingStyle(Style style)
        => ReferenceEquals(Style, style) ? this : Extend(style: style, size: SizeAtStyle(TextSize, style));

    public Options HavingCrampedStyle() => HavingStyle(Style.Cramp());

    public Options HavingSize(int size)
        => Size == size && TextSize == size
            ? this
            : Extend(style: Style.Text(), size: size, textSize: size, sizeMultiplier: SizeMultipliers[size - 1]);

    public Options HavingBaseStyle(Style style = null)
    {
        style ??= Style.Text();
        int wantSize = SizeAtStyle(BaseSize, style);
        if (Size == wantSize && TextSize == BaseSize && ReferenceEquals(Style, style))
            return this;
        return Extend(style: style, size: wantSize);
    }

    public Options HavingBaseSizing()
    {
        int size = Style.Id switch
        {
            4 or 5 => 3,
            6 or 7 => 1,
            _ => 6,
        };
        return Extend(style: Style.Text(), size: size);
    }

    public Options WithColor(string color) => Extend(color: color);
    public Options WithPhantom() => Extend(phantom: true);
    public Options WithFont(string font) => Extend(font: font);
    public Options WithTextFontFamily(string fontFamily) => Extend(fontFamily: fontFamily, font: "");
    public Options WithTextFontWeight(string fontWeight) => Extend(fontWeight: fontWeight, font: "");
    public Options WithTextFontShape(string fontShape) => Extend(fontShape: fontShape, font: "");

    public List<string> SizingClasses(Options oldOptions)
        => oldOptions.Size != Size
            ? new List<string> { "katex-sizing", "reset-size" + oldOptions.Size, "size" + Size }
            : new List<string>();

    public List<string> BaseSizingClasses()
        => Size != BaseSize
            ? new List<string> { "katex-sizing", "reset-size" + Size, "size" + BaseSize }
            : new List<string>();

    public FontMetrics FontMetrics()
    {
        _fontMetrics ??= FontMetricsData.GetGlobalMetrics(Size);
        return _fontMetrics;
    }

    public string GetColor() => Phantom ? "transparent" : Color;
}
